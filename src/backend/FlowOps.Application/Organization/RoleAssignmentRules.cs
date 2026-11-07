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
