using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Organization;

/// <summary>
/// Rules about who holds which role, checked against the database (ADR-0012 §2). Shared by the immediate role edit and
/// deactivation (<see cref="UserAdministrationService"/>) and by approving a pending change (<see cref="RoleChangeService"/>),
/// so the rules have one home. Never saves.
/// </summary>
public static class RoleAssignmentRules
{
    /// <summary>Active users holding <c>Administrator</c>, from the database (session claims can be a minute stale).</summary>
    public static IQueryable<UserReadModel> ActiveAdministrators(IAppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Users.Where(u => u.IsActive && db.UserRoles.Any(r => r.UserId == u.Id && r.RoleName == Roles.Administrator));
    }

    /// <summary>
    /// 403 <c>auth.forbidden</c> unless <paramref name="actorId"/> is an active administrator in the database. Every admin
    /// write calls it first: the Admin policy reads session claims, which stay up to a minute stale after a demotion or
    /// deactivation (#76).
    /// </summary>
    public static async Task EnsureActiveAdministratorAsync(IAppDbContext db, Guid actorId, CancellationToken ct)
    {
        if (!await ActiveAdministrators(db).AnyAsync(u => u.Id == actorId, ct))
        {
            throw new ForbiddenException();
        }
    }

    /// <summary>Active administrators other than <paramref name="userId"/> who could approve a change proposed at <paramref name="proposedAt"/> (ADR-0013).</summary>
    public static IQueryable<UserReadModel> PossibleApprovers(IAppDbContext db, Guid userId, DateTimeOffset proposedAt)
    {
        var createdBy = proposedAt - RoleChange.MinimumApproverAccountAge;
        return ActiveAdministrators(db).Where(u => u.Id != userId && u.CreatedAt <= createdBy);
    }

    public static async Task<string[]> CurrentRolesAsync(IAppDbContext db, Guid userId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.RoleName).ToListAsync(ct);
        return [.. roles.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Checks replacing <paramref name="currentRoles"/> with <paramref name="newRoles"/>: <c>Administrator</c> can't be added
    /// to an existing account (422 <c>user.admin_role_on_create_only</c>), and an active administrator must remain
    /// (422 <c>user.last_admin</c>).
    /// </summary>
    public static async Task EnsureAllowedAsync(
        IAppDbContext db, Guid userId, IReadOnlyCollection<string> currentRoles, IReadOnlyCollection<string> newRoles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currentRoles);
        ArgumentNullException.ThrowIfNull(newRoles);
        var isAdmin = currentRoles.Contains(Roles.Administrator);
        var staysAdmin = newRoles.Contains(Roles.Administrator);
        if (staysAdmin && !isAdmin)
        {
            throw new BusinessRuleException(ErrorCodes.UserAdminRoleOnCreateOnly,
                "The Administrator role can only be given when a new account is created.");
        }

        if (isAdmin && !staysAdmin)
        {
            await EnsureNotLastAdministratorAsync(db, userId, ct);
        }
    }

    /// <summary>Called before the user stops being an active Administrator. Phase 1 accepts the race of two simultaneous demotions.</summary>
    public static async Task EnsureNotLastAdministratorAsync(IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var activeAdmins = ActiveAdministrators(db);
        var isActiveAdmin = await activeAdmins.AnyAsync(u => u.Id == userId, ct);
        if (isActiveAdmin && !await activeAdmins.AnyAsync(u => u.Id != userId, ct))
        {
            throw new BusinessRuleException(ErrorCodes.UserLastAdmin, "At least one active administrator must remain.");
        }
    }

    public static bool SameRoles(IEnumerable<string> a, IEnumerable<string> b) =>
        a.Order(StringComparer.Ordinal).SequenceEqual(b.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
