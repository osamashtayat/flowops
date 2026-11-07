using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowOps.Application.Organization;

/// <summary>
/// User administration (API_CONTRACT.md §8). Rules: no manager cycles (I13), an active Administrator always
/// remains, inactive departments take no new members, Administrator is only given to new accounts and an admin's own
/// role change waits for another admin (ADR-0012). Duplicate emails surface as 409 from the unique index.
/// </summary>
public sealed partial class UserAdministrationService(
    IAppDbContext db, IIdentityAdmin identity, RoleChangeReader roleChanges, ICurrentUser currentUser, TimeProvider time,
    PostCommitPublisher realtime, ILogger<UserAdministrationService> logger)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var users = db.Users.AsNoTracking();
        if (!query.IncludeInactive)
        {
            users = users.Where(u => u.IsActive);
        }

        if (query.DepartmentId is { } departmentId)
        {
            users = users.Where(u => u.DepartmentId == departmentId);
        }

        if (query.Role is { } role)
        {
            var roleName = role.ToString();
            users = users.Where(u => db.UserRoles.Any(r => r.UserId == u.Id && r.RoleName == roleName));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            // SQL, not .NET: lower(...) LIKE '%…%' with the pattern escaped by EF. Npgsql translates only ToLower(),
            // and neither culture overloads nor StringComparison translate, hence the suppressions.
#pragma warning disable CA1304, CA1311, CA1862
            users = users.Where(u => u.DisplayName.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var page = await Rows(users.OrderBy(u => u.DisplayName).ThenBy(u => u.Id)).ToPagedResultAsync(query, ct);
        var reports = await DirectReportCountsAsync([.. page.Items.Select(r => r.Id)], ct);
        return page.Map(row => row.ToDto(reports.GetValueOrDefault(row.Id)));
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await Rows(db.Users.AsNoTracking().Where(u => u.Id == id)).SingleOrDefaultAsync(ct) ?? throw new NotFoundException();
        var reports = await DirectReportCountsAsync([id], ct);
        return row.ToDto(reports.GetValueOrDefault(id));
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await EnsureDepartmentCanReceiveAsync(body.DepartmentId, ct);
        if (body.ManagerId is { } managerId)
        {
            EnsureNotSelfManagerAssignment(Guid.Empty, managerId);
            // A brand-new user has no reports, so only an unknown or inactive manager can be wrong here.
            await EnsureActiveManagerAsync(managerId, ct);
        }

        await EnsurePasswordAcceptedAsync(body.InitialPassword, "initialPassword", ct);
        var account = new NewUserAccount(body.Email.Trim(), body.DisplayName.Trim(), body.InitialPassword, body.DepartmentId,
            body.ManagerId, RoleNames(body.Roles), time.GetUtcNow());
        var id = await identity.StageNewUserAsync(account, ct);
        await db.SaveChangesAsync(ct);
        LogCreated(logger, currentUser.Id, id, account.Roles, body.DepartmentId, body.ManagerId);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var current = await db.Users.Where(u => u.Id == id)
            .Select(u => new { u.DepartmentId, u.ManagerId, u.IsActive })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException();

        if (body.DepartmentId != current.DepartmentId)
        {
            await EnsureDepartmentCanReceiveAsync(body.DepartmentId, ct);
        }

        EnsureNoSelfManagerLink(id, current.ManagerId, current.IsActive, body);
        if (body.ManagerId is { } managerId && managerId != current.ManagerId)
        {
            await ManagerAssignmentRules.EnsureNoCycleAsync(db, id, managerId, ct);
            await EnsureActiveManagerAsync(managerId, ct);
        }

        if (current.IsActive && !body.IsActive)
        {
            await RoleAssignmentRules.EnsureNotLastAdministratorAsync(db, id, ct);
            EnsureNotSelfDeactivation(id);
        }

        await identity.StageProfileAsync(id, new UserProfile(body.DisplayName.Trim(), body.DepartmentId, body.ManagerId, body.IsActive), ct);
        await SaveAsync(ct);
        LogProfileUpdated(logger, currentUser.Id, id, body.DepartmentId, body.ManagerId, body.IsActive);
        if (current.IsActive && !body.IsActive)
        {
            await realtime.SessionsEndedAsync(id);
        }

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Replaces another user's roles at once. An administrator's own roles only change after another administrator
    /// approves (ADR-0012 §3), so for the caller this records a pending <see cref="RoleChange"/> instead.
    /// </summary>
    public async Task<RoleUpdateOutcome> UpdateRolesAsync(Guid id, UpdateUserRolesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await EnsureExistsAsync(id, ct);
        var roles = RoleNames(body.Roles);
        // Own roles only become a proposal (approval re-checks them); others are claimed so a parallel edit can't be overtaken.
        var currentRoles = id == currentUser.Id
            ? await RoleAssignmentRules.CurrentRolesAsync(db, id, ct)
            : await identity.ClaimRolesForChangeAsync(id, ct);
        if (RoleAssignmentRules.SameRoles(currentRoles, roles))
        {
            return new RoleUpdateOutcome(await GetAsync(id, ct), null);
        }

        await RoleAssignmentRules.EnsureAllowedAsync(db, id, currentRoles, roles, ct);
        if (id == currentUser.Id)
        {
            return new RoleUpdateOutcome(null, await ProposeOwnRoleChangeAsync(currentRoles, roles, ct));
        }

        await identity.StageRolesAsync(id, roles, ct);
        await SaveAsync(ct);
        LogRolesReplaced(logger, currentUser.Id, id, roles);
        return new RoleUpdateOutcome(await GetAsync(id, ct), null);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await EnsureExistsAsync(id, ct);
        if (id == currentUser.Id)
        {
            // Decided 2026-11-06 (#31): the admin reset skips the current-password check that change-password requires.
            throw SelfAdministration("Use change password to change your own password.");
        }

        await EnsurePasswordAcceptedAsync(body.NewPassword, "newPassword", ct);
        await identity.StagePasswordResetAsync(id, body.NewPassword, ct);
        await SaveAsync(ct);
        LogPasswordReset(logger, currentUser.Id, id);
        await realtime.SessionsEndedAsync(id);
    }

    private async Task<RoleChangeDto> ProposeOwnRoleChangeAsync(IReadOnlyList<string> currentRoles, string[] roles, CancellationToken ct)
    {
        var reviewers = await RoleAssignmentRules.ActiveAdministrators(db).Where(u => u.Id != currentUser.Id).Select(u => u.Id).ToListAsync(ct);
        if (reviewers.Count == 0)
        {
            throw new BusinessRuleException(ErrorCodes.RoleChangeNoReviewer,
                "You are the only active administrator, so nobody could approve a change to your own roles.");
        }

        var now = time.GetUtcNow();
        if (await db.RoleChanges.SingleOrDefaultAsync(c => c.UserId == currentUser.Id && c.Status == RoleChangeStatus.Pending, ct)
            is { } pending && !pending.ExpireIfDue(now))
        {
            throw new ConflictException(ErrorCodes.RoleChangeAlreadyPending, "You already have a role change waiting for approval.");
        }

        var change = RoleChange.Propose(currentUser.Id, currentRoles, roles, now);
        db.RoleChanges.Add(change);
        var proposerName = await db.Users.Where(u => u.Id == currentUser.Id).Select(u => u.DisplayName).SingleAsync(ct);
        var notifications = NotificationFactory.RoleChangePending(change, proposerName, reviewers, now).ToList();
        db.Notifications.AddRange(notifications);
        await SaveAsync(ct);
        LogRoleChangeProposed(logger, currentUser.Id, change.Id, roles);
        await realtime.NotificationsAsync(notifications);
        return await roleChanges.GetAsync(change.Id, ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another change (e.g. the user's own password change) updated the account first.
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, "The user was changed by someone else. Reload and try again.");
        }
    }

    /// <summary>
    /// Administrator grants no process authority (AUTHORIZATION.md §2), so an admin may not use admin powers to get it:
    /// no changing their own manager link and no making themselves anyone's manager (decided 2026-11-06).
    /// </summary>
    private void EnsureNotSelfManagerAssignment(Guid userId, Guid? newManagerId)
    {
        if (userId == currentUser.Id)
        {
            throw SelfAdministration("You cannot change your own manager. Ask another administrator.");
        }

        if (newManagerId == currentUser.Id)
        {
            throw SelfAdministration("You cannot make yourself someone's manager. Ask another administrator.");
        }
    }

    private void EnsureNoSelfManagerLink(Guid id, Guid? currentManagerId, bool wasActive, UpdateUserRequest body)
    {
        if (body.ManagerId != currentManagerId)
        {
            EnsureNotSelfManagerAssignment(id, body.ManagerId);
        }
        else if (!wasActive && body.IsActive && body.ManagerId == currentUser.Id)
        {
            // Reactivating someone who still reports to the caller would make the caller their manager again.
            throw SelfAdministration("You cannot reactivate someone who reports to you. Ask another administrator.");
        }
    }

    /// <summary>Decided 2026-11-06 (issue #27): another administrator has to deactivate an admin's account.</summary>
    private void EnsureNotSelfDeactivation(Guid userId)
    {
        if (userId == currentUser.Id)
        {
            throw SelfAdministration("You cannot deactivate your own account. Ask another administrator.");
        }
    }

    private static BusinessRuleException SelfAdministration(string message) => new(ErrorCodes.UserSelfAdministration, message);

    private async Task EnsureExistsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, ct))
        {
            throw new NotFoundException();
        }
    }

    private async Task EnsureDepartmentCanReceiveAsync(Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is not { } id)
        {
            return;
        }

        var isActive = await db.Departments.Where(d => d.Id == id).Select(d => (bool?)d.IsActive).SingleOrDefaultAsync(ct)
            ?? throw ValidationFailedException.For("departmentId", "The department does not exist.");
        if (!isActive)
        {
            throw new BusinessRuleException(ErrorCodes.DepartmentInactive, "The department is inactive and cannot receive new members.");
        }
    }

    private async Task EnsureActiveManagerAsync(Guid managerId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == managerId && u.IsActive, ct))
        {
            throw ValidationFailedException.For("managerId", "The manager must be an existing, active user.");
        }
    }

    private async Task EnsurePasswordAcceptedAsync(string password, string field, CancellationToken ct)
    {
        var problems = await identity.CheckPasswordAsync(password, ct);
        if (problems.Count > 0)
        {
            throw ValidationFailedException.For(field, [.. problems]);
        }
    }

    private static string[] RoleNames(IEnumerable<Role> roles) => [.. roles.Distinct().Order().Select(r => r.ToString())];

    private async Task<Dictionary<Guid, int>> DirectReportCountsAsync(Guid[] managerIds, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(ManagerRelationship.IsActiveDirectReport)
            .Where(u => managerIds.Contains(u.ManagerId!.Value))
            .GroupBy(u => u.ManagerId!.Value)
            .Select(g => new { ManagerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ManagerId, g => g.Count, ct);

    private IQueryable<UserRow> Rows(IQueryable<UserReadModel> users) => users.Select(u => new UserRow(
        u.Id,
        u.Email,
        u.DisplayName,
        u.IsActive,
        u.MustChangePassword,
        db.Departments.Where(d => d.Id == u.DepartmentId).Select(d => new DepartmentRef(d.Id, d.Name)).SingleOrDefault(),
        db.Users.Where(m => m.Id == u.ManagerId).Select(m => new UserRef(m.Id, m.DisplayName)).SingleOrDefault(),
        db.UserRoles.Where(r => r.UserId == u.Id).Select(r => r.RoleName).ToList(),
        u.CreatedAt));

    // Audit trail of administrative changes (who changed whom, and what): ids, role names and flags only, never passwords.
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Admin {ActorId} created user {UserId} with roles {Roles}, department {DepartmentId}, manager {ManagerId}")]
    private static partial void LogCreated(ILogger logger, Guid actorId, Guid userId, IEnumerable<string> roles, Guid? departmentId, Guid? managerId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Admin {ActorId} updated user {UserId}: department {DepartmentId}, manager {ManagerId}, active {IsActive}")]
    private static partial void LogProfileUpdated(ILogger logger, Guid actorId, Guid userId, Guid? departmentId, Guid? managerId, bool isActive);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} set roles of user {UserId} to {Roles}")]
    private static partial void LogRolesReplaced(ILogger logger, Guid actorId, Guid userId, IEnumerable<string> roles);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} asked to change their own roles to {Roles} (role change {RoleChangeId})")]
    private static partial void LogRoleChangeProposed(ILogger logger, Guid actorId, Guid roleChangeId, IEnumerable<string> roles);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} reset the password of user {UserId}")]
    private static partial void LogPasswordReset(ILogger logger, Guid actorId, Guid userId);

    private sealed record UserRow(
        Guid Id, string Email, string DisplayName, bool IsActive, bool MustChangePassword,
        DepartmentRef? Department, UserRef? Manager, List<string> Roles, DateTimeOffset CreatedAt)
    {
        public UserDto ToDto(int directReportCount) => new(
            Id, Email, DisplayName, IsActive, MustChangePassword, Department, Manager,
            [.. Roles.Select(Enum.Parse<Role>).Order()], directReportCount, CreatedAt);
    }
}
