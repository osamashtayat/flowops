using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Requests;

/// <summary>
/// Who a step is assigned to (WORKFLOWS.md §1.1), the single home of that rule. RequesterManager → the requester's
/// manager, who must exist, be active and not be the requester (I12, else 422). Role(R) → the role queue; never fails
/// at assignment time (eligibility is checked at decision time).
/// </summary>
public static class TaskAssigneeResolver
{
    /// <summary>
    /// The submitter as submission needs them: server-side department and manager (I14), plus their current active flag
    /// and roles from the database, because the session's claims can be up to a minute stale (#77).
    /// </summary>
    public sealed record Requester(
        Guid Id, string DisplayName, Guid? DepartmentId, Guid? ManagerId, bool ManagerIsActive, bool IsActive, IReadOnlyCollection<string> Roles);

    public static Task<Requester?> LoadRequesterAsync(IAppDbContext db, Guid requesterId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Users.AsNoTracking().Where(u => u.Id == requesterId)
            .Select(u => new Requester(u.Id, u.DisplayName, u.DepartmentId, u.ManagerId,
                db.Users.Any(m => m.Id == u.ManagerId && m.IsActive), u.IsActive,
                db.UserRoles.Where(r => r.UserId == u.Id).Select(r => r.RoleName).ToList()))
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>I12: the manager who will approve the first step. Throws 422 <c>request.manager_not_assigned</c>.</summary>
    public static Guid RequireManager(Requester requester)
    {
        ArgumentNullException.ThrowIfNull(requester);
        return requester is { ManagerId: { } managerId, ManagerIsActive: true } && managerId != requester.Id
            ? managerId
            : throw new BusinessRuleException(ErrorCodes.RequestManagerNotAssigned,
                "You have no active manager to approve this request. Ask an administrator to assign one.");
    }

    /// <summary>
    /// The resolver the aggregate takes for submission and decisions alike (#83): RequesterManager steps go to the
    /// requester's current manager (I12, checked only when such a step is reached), role steps to the role queue.
    /// </summary>
    public static Func<ProcessStep, Assignee> ResolverFor(Requester requester)
    {
        ArgumentNullException.ThrowIfNull(requester);
        return step => Resolve(step, step.Assignee.IsRequesterManager ? RequireManager(requester) : Guid.Empty);
    }

    /// <summary>Resolves any step; <paramref name="requesterManagerId"/> is used for RequesterManager steps.</summary>
    public static Assignee Resolve(ProcessStep step, Guid requesterManagerId)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.Assignee.IsRequesterManager ? Assignee.ForUser(requesterManagerId) : Assignee.ForRole(step.Assignee.Role!);
    }
}
