using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowOps.Application.Organization;

/// <summary>
/// User administration (API_CONTRACT.md §8). Rules: no manager cycles (I13), an active Administrator always
/// remains, inactive departments take no new members. Duplicate emails surface as 409 from the unique index.
/// </summary>
public sealed partial class UserAdministrationService(
    IAppDbContext db, IIdentityAdmin identity, ICurrentUser currentUser, TimeProvider time, ILogger<UserAdministrationService> logger)
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
            // A brand-new user has no reports, so only an unknown or inactive manager can be wrong here.
            await EnsureActiveManagerAsync(managerId, ct);
        }

        await EnsurePasswordAcceptedAsync(body.InitialPassword, "initialPassword", ct);
        var account = new NewUserAccount(body.Email.Trim(), body.DisplayName.Trim(), body.InitialPassword, body.DepartmentId,
            body.ManagerId, RoleNames(body.Roles), time.GetUtcNow());
        var id = await identity.StageNewUserAsync(account, ct);
        await db.SaveChangesAsync(ct);
        LogCreated(logger, currentUser.Id, id, account.Roles);
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

        if (body.ManagerId is { } managerId && managerId != current.ManagerId)
        {
            await ManagerAssignmentRules.EnsureNoCycleAsync(db, id, managerId, ct);
            await EnsureActiveManagerAsync(managerId, ct);
        }

        if (current.IsActive && !body.IsActive)
        {
            await EnsureNotLastAdministratorAsync(id, ct);
        }

        await identity.StageProfileAsync(id, new UserProfile(body.DisplayName.Trim(), body.DepartmentId, body.ManagerId, body.IsActive), ct);
        await SaveAsync(ct);
        LogProfileUpdated(logger, currentUser.Id, id, body.DepartmentId, body.ManagerId, body.IsActive);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> UpdateRolesAsync(Guid id, UpdateUserRolesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await EnsureExistsAsync(id, ct);
        var roles = RoleNames(body.Roles);
        if (!roles.Contains(Roles.Administrator))
        {
            await EnsureNotLastAdministratorAsync(id, ct);
        }

        await identity.StageRolesAsync(id, roles, ct);
        await SaveAsync(ct);
        LogRolesReplaced(logger, currentUser.Id, id, roles);
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await EnsureExistsAsync(id, ct);
        await EnsurePasswordAcceptedAsync(body.NewPassword, "newPassword", ct);
        await identity.StagePasswordResetAsync(id, body.NewPassword, ct);
        await SaveAsync(ct);
        LogPasswordReset(logger, currentUser.Id, id);
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

    /// <summary>Called before the user stops being an active Administrator. Phase 1 accepts the race of two simultaneous demotions.</summary>
    private async Task EnsureNotLastAdministratorAsync(Guid id, CancellationToken ct)
    {
        var activeAdmins = db.Users.Where(u => u.IsActive && db.UserRoles.Any(r => r.UserId == u.Id && r.RoleName == Roles.Administrator));
        var isActiveAdmin = await activeAdmins.AnyAsync(u => u.Id == id, ct);
        if (isActiveAdmin && !await activeAdmins.AnyAsync(u => u.Id != id, ct))
        {
            throw new BusinessRuleException(ErrorCodes.UserLastAdmin, "At least one active administrator must remain.");
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
    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} created user {UserId} with roles {Roles}")]
    private static partial void LogCreated(ILogger logger, Guid actorId, Guid userId, IEnumerable<string> roles);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Admin {ActorId} updated user {UserId}: department {DepartmentId}, manager {ManagerId}, active {IsActive}")]
    private static partial void LogProfileUpdated(ILogger logger, Guid actorId, Guid userId, Guid? departmentId, Guid? managerId, bool isActive);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} set roles of user {UserId} to {Roles}")]
    private static partial void LogRolesReplaced(ILogger logger, Guid actorId, Guid userId, IEnumerable<string> roles);

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
