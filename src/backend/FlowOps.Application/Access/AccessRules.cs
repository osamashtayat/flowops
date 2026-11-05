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
/// Administrator grants nothing: the domain never assigns a task to that role. An inactive user sees nothing.
/// </summary>
public sealed class AccessRules(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>The signed-in caller's context: roles and active flag read from the database, never from claims.</summary>
    public Task<AccessContext> ForCurrentUserAsync(CancellationToken ct) => ContextForAsync(currentUser.Id, ct);

    /// <summary>Test seam; production code uses <see cref="ForCurrentUserAsync"/> so the actor can only be the caller.</summary>
    internal async Task<AccessContext> ContextForAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, Roles = db.UserRoles.Where(r => r.UserId == u.Id).Select(r => r.RoleName).ToList() })
            .SingleOrDefaultAsync(ct);
        return user is null ? new AccessContext(userId, [], isActive: false) : new AccessContext(userId, user.Roles, user.IsActive);
    }

    /// <summary>
    /// Filters <paramref name="requests"/> (tracked or not, with or without includes) to those <paramref name="me"/> can see.
    /// One SQL query. Load requests for commands through this too, so "not visible" is "not found" (404) by construction.
    /// </summary>
    public IQueryable<Request> VisibleTo(IQueryable<Request> requests, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(me);
        var userId = me.UserId;
        var isActive = me.IsActive;
        var roles = me.RoleArray;
        return requests.Where(r => isActive && (
            r.RequesterId == userId
            || db.Users.Any(u => u.Id == r.RequesterId && u.ManagerId == userId)
            || r.Tasks.Any(t => t.AssigneeUserId == userId
                || (t.AssigneeRole != null && roles.Contains(t.AssigneeRole))
                || t.DecidedById == userId)));
    }

    public Task<bool> CanViewAsync(Guid requestId, AccessContext me, CancellationToken ct) =>
        VisibleTo(db.Requests, me).AnyAsync(r => r.Id == requestId, ct);
}
