using System.Diagnostics;
using System.Linq.Expressions;
using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Access;

/// <summary>
/// The one implementation of "who may decide a task" (AUTHORIZATION.md §4), used for decisions, for
/// <c>availableActions</c> and for the inbox. User U may decide task T iff T is assigned to U or to a role U holds
/// (T1/T2), U is active, U is not the requester (I2), and U decided no other task of the same request (T3, separation of
/// duties, #50): every step of a request is decided by a different person. Whether T is still open and current is the domain's
/// check (409s), so a late decider gets "already decided", not "not eligible".
/// Eligibility says nothing about visibility: callers load the request through <see cref="AccessRules.VisibleTo"/>
/// first, so an outsider gets 404 and never learns the request exists.
/// </summary>
public static class TaskEligibility
{
    /// <summary>T1/T2 as a translatable predicate: the task is assigned to <paramref name="me"/> or to one of their roles.</summary>
    public static Expression<Func<WorkTask, bool>> AssignedTo(AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(me);
        var userId = me.UserId;
        var roles = me.RoleArray;
        return t => t.AssigneeUserId == userId || (t.AssigneeRole != null && roles.Contains(t.AssigneeRole));
    }

    /// <summary>
    /// Open tasks <paramref name="me"/> may decide now, with their request (the inbox's "Open" list), so callers can
    /// project both in one query. Visible by construction: an assigned task is R3.
    /// </summary>
    public static IQueryable<DecidableTask> DecidableBy(IQueryable<Request> requests, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(me);
        var userId = me.UserId;
        var isActive = me.IsActive;
        return requests
            .Where(r => isActive && r.RequesterId != userId && !r.Tasks.Any(t => t.DecidedById == userId))
            .SelectMany(r => r.Tasks.AsQueryable().Where(t => t.Status == WorkTaskStatus.Open).Where(AssignedTo(me)),
                (r, t) => new DecidableTask { Request = r, Task = t });
    }

    /// <summary>
    /// The users who may decide <paramref name="task"/> of <paramref name="request"/>: active, assigned (the user, or a holder
    /// of the role), not the requester, and not someone who decided another of its tasks. Used to address notifications
    /// (WORKFLOWS.md §1.4); the same conditions as <see cref="FailureCode"/>. Earlier deciders come from the loaded
    /// aggregate, so a decision made in the current unit of work (not yet saved) already counts.
    /// </summary>
    public static IQueryable<Guid> EligibleDeciders(IAppDbContext db, Request request, WorkTask task)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(task);
        var requesterId = request.RequesterId;
        var assigneeUserId = task.AssigneeUserId;
        var assigneeRole = task.AssigneeRole;
        var earlierDeciders = DecidersOf(request.Tasks.Where(t => t.Id != task.Id).Select(t => t.DecidedById));
        return db.Users
            .Where(u => u.IsActive && u.Id != requesterId && !earlierDeciders.Contains(u.Id))
            .Where(u => u.Id == assigneeUserId || (assigneeRole != null && db.UserRoles.Any(r => r.UserId == u.Id && r.RoleName == assigneeRole)))
            .Select(u => u.Id);
    }

    /// <summary>
    /// Open tasks nobody may decide right now (WORKFLOWS.md §1.1, §4): the negation of <see cref="EligibleDeciders"/>, with
    /// the same conditions (active, assigned user or role holder, not the requester, decided no other task of the request). A test over the seeded org
    /// checks the two agree. A user assignee can only fail by being inactive, so that is the reason; otherwise the role
    /// queue has no eligible member.
    /// </summary>
    public static IQueryable<StalledTask> Stalled(IAppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Requests
            .SelectMany(r => r.Tasks.Where(t => t.Status == WorkTaskStatus.Open), (r, t) => new { Request = r, Task = t })
            .Where(x => !db.Users.Any(u => u.IsActive && u.Id != x.Request.RequesterId
                && !x.Request.Tasks.Any(t => t.DecidedById == u.Id)
                && (u.Id == x.Task.AssigneeUserId
                    || (x.Task.AssigneeRole != null && db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleName == x.Task.AssigneeRole)))))
            .Select(x => new StalledTask { Request = x.Request, Task = x.Task });
    }

    /// <summary>
    /// Why <paramref name="me"/> may not decide <paramref name="task"/> of a request by <paramref name="requesterId"/>, or
    /// <c>null</c>. <paramref name="deciderIds"/> are the <c>DecidedById</c> values of all the request's tasks, this one included.
    /// </summary>
    public static string? FailureCode(Guid requesterId, IEnumerable<Guid?> deciderIds, WorkTask task, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(deciderIds);
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(me);
        if (!me.IsActive || !me.IsAssigned(task))
        {
            return ErrorCodes.TaskNotEligible;
        }

        // Checked after assignment, so only someone who would otherwise be eligible learns it's their own request.
        if (requesterId == me.UserId)
        {
            return ErrorCodes.TaskSelfDecision;
        }

        // Only decisions on *other* tasks count: re-deciding your own task is the domain's 409 "already decided".
        var ownDecisionsElsewhere = deciderIds.Count(id => id == me.UserId) - (task.DecidedById == me.UserId ? 1 : 0);
        return ownDecisionsElsewhere > 0 ? ErrorCodes.TaskAlreadyParticipated : null;
    }

    /// <summary>
    /// Throws 403 with <c>task.not_eligible</c>, <c>task.self_decision</c> or <c>task.already_participated</c>. Call after
    /// the visibility check, with the request's tasks loaded.
    /// </summary>
    public static void EnsureCanDecide(Request request, WorkTask task, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = FailureCode(request.RequesterId, request.Tasks.Select(t => t.DecidedById), task, me);
        if (code is not null)
        {
            throw new ForbiddenException(code, code switch
            {
                ErrorCodes.TaskSelfDecision => "You cannot decide a task on your own request.",
                ErrorCodes.TaskAlreadyParticipated => "You decided an earlier step of this request. Another person has to decide this one.",
                _ => "You are not allowed to decide this task.",
            });
        }
    }

    /// <summary>
    /// The caller's buttons for a request's open task, or <c>null</c> when they have none. Takes only what a
    /// projection can supply (requester id, who decided its tasks, and the open task), so read paths needn't load the aggregate.
    /// </summary>
    public static AvailableActionsDto? AvailableActions(Guid requesterId, IEnumerable<Guid?> deciderIds, WorkTask? openTask, AccessContext me) =>
        openTask is { IsOpen: true } && FailureCode(requesterId, deciderIds, openTask, me) is null
            ? new AvailableActionsDto(openTask.Id, [.. openTask.AllowedActions.Select(ToDto)], VersionToken.Format(openTask.Version))
            : null;

    private static List<Guid> DecidersOf(IEnumerable<Guid?> deciderIds) =>
        [.. deciderIds.Where(id => id is not null).Select(id => id!.Value).Distinct()];

    private static TaskAction ToDto(TaskDecisionAction action) => action switch
    {
        TaskDecisionAction.Approve => TaskAction.Approve,
        TaskDecisionAction.Reject => TaskAction.Reject,
        TaskDecisionAction.Fulfill => TaskAction.Fulfill,
        _ => throw new UnreachableException($"Unknown decision action {action}."),
    };
}

/// <summary>An open task nobody can decide, with its request; project both in one query.</summary>
public sealed class StalledTask
{
    public required Request Request { get; init; }

    public required WorkTask Task { get; init; }
}

/// <summary>An open task the caller may decide, with its request; project both in one query.</summary>
public sealed class DecidableTask
{
    public required Request Request { get; init; }

    public required WorkTask Task { get; init; }
}
