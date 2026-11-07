using System.Linq.Expressions;
using FlowOps.Application.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Access;

/// <summary>
/// The one implementation of "who can see a request" (AUTHORIZATION.md §4) and of who may read a leave reason (§5).
/// The same filters back lists and single-item checks, so they can never disagree. Never saves. Visible to user U iff
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
        return requests.Where(Active(me, AnyOf(IsRequester(me), IsRequestersManager(me), IsParticipant(me))));
    }

    public Task<bool> CanViewAsync(Guid requestId, AccessContext me, CancellationToken ct) =>
        VisibleTo(db.Requests, me).AnyAsync(r => r.Id == requestId, ct);

    /// <summary>
    /// §5: the leave reason goes to the requester (R1), their direct manager (R2) and HR officers who see the request
    /// through its HR task (HrOfficer + R3 via the HR queue). Being a participant for another reason, such as having decided
    /// the manager step as a former manager, doesn't count. Everyone else gets <c>reason: null, reasonHidden: true</c>.
    /// </summary>
    public Task<bool> CanReadLeaveReasonAsync(Guid requestId, AccessContext me, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(me);
        var hrParticipant = me.Roles.Contains(Roles.HrOfficer) ? HasHrTask() : r => false;
        return db.Requests.Where(r => r.Id == requestId)
            .AnyAsync(Active(me, AnyOf(IsRequester(me), IsRequestersManager(me), hrParticipant)), ct);
    }

    /// <summary>
    /// The users who can currently see <paramref name="requestId"/>: R1–R3 read from the request's side, for addressing
    /// <c>requestChanged</c> (API_CONTRACT.md §9). It must stay the same set as <see cref="VisibleTo"/>; a table test over the
    /// seeded org checks every user against <see cref="CanViewAsync"/>. Active users only; administrators get nothing.
    /// </summary>
    public IQueryable<Guid> ViewerIds(Guid requestId) =>
        db.Users
            .Where(u => u.IsActive)
            .Where(u => db.Requests.Where(r => r.Id == requestId).Any(r =>
                r.RequesterId == u.Id // R1
                || db.Users.Any(q => q.Id == r.RequesterId && q.ManagerId == u.Id) // R2
                || r.Tasks.Any(t => t.AssigneeUserId == u.Id || t.DecidedById == u.Id // R3
                    || (t.AssigneeRole != null && db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleName == t.AssigneeRole)))))
            .Select(u => u.Id);

    // R1
    private static Expression<Func<Request, bool>> IsRequester(AccessContext me)
    {
        var userId = me.UserId;
        return r => r.RequesterId == userId;
    }

    // R2
    private Expression<Func<Request, bool>> IsRequestersManager(AccessContext me)
    {
        var userId = me.UserId;
        return r => db.Users.Any(u => u.Id == r.RequesterId && u.ManagerId == userId);
    }

    // R3
    private static Expression<Func<Request, bool>> IsParticipant(AccessContext me)
    {
        var userId = me.UserId;
        var roles = me.RoleArray;
        return r => r.Tasks.Any(t => t.AssigneeUserId == userId
            || (t.AssigneeRole != null && roles.Contains(t.AssigneeRole))
            || t.DecidedById == userId);
    }

    // The part of R3 an HR officer gets through the HR queue.
    private static Expression<Func<Request, bool>> HasHrTask() => r => r.Tasks.Any(t => t.AssigneeRole == Roles.HrOfficer);

    private static Expression<Func<Request, bool>> Active(AccessContext me, Expression<Func<Request, bool>> rule)
    {
        var isActive = me.IsActive;
        return Predicates.And(_ => isActive, rule);
    }

    private static Expression<Func<Request, bool>> AnyOf(params Expression<Func<Request, bool>>[] rules) =>
        rules.Aggregate(Predicates.Or);
}
