using FlowOps.Application.Common;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Access;

/// <summary>
/// The one implementation of "who can see a request" (AUTHORIZATION.md §4). The same filter backs lists and
/// single-item checks, so they can never disagree. Never saves. A request is visible to user U iff
/// <list type="bullet">
/// <item>R1: U is the requester;</item>
/// <item>R2: U is the requester's current direct manager;</item>
/// <item>R3: some task on it is assigned to U, to a role U holds, or was decided by U.</item>
/// </list>
/// Administrator grants nothing here: no task is ever assigned to that role.
/// </summary>
public sealed class AccessRules(IAppDbContext db)
{
    /// <summary>The caller's id, current roles and active flag, read from the database.</summary>
    public async Task<AccessContext> ContextForAsync(Guid userId, CancellationToken ct)
    {
        var isActive = await db.Users.Where(u => u.Id == userId).Select(u => u.IsActive).SingleOrDefaultAsync(ct);
        var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.RoleName).ToListAsync(ct);
        return new AccessContext(userId, roles.ToHashSet(StringComparer.Ordinal), isActive);
    }

    /// <summary>Filters <paramref name="requests"/> (tracked or not, with or without includes) to those <paramref name="me"/> can see. One SQL query.</summary>
    public IQueryable<Request> VisibleTo(IQueryable<Request> requests, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(me);
        if (!me.IsActive)
        {
            return requests.Where(_ => false);
        }

        var userId = me.UserId;
        var roles = me.RoleArray;
        return requests.Where(r =>
            r.RequesterId == userId
            || db.Users.Any(u => u.Id == r.RequesterId && u.ManagerId == userId)
            || r.Tasks.Any(t => t.AssigneeUserId == userId
                || (t.AssigneeRole != null && roles.Contains(t.AssigneeRole))
                || t.DecidedById == userId));
    }

    public Task<bool> CanViewAsync(Guid requestId, AccessContext me, CancellationToken ct) =>
        VisibleTo(db.Requests, me).AnyAsync(r => r.Id == requestId, ct);
}
