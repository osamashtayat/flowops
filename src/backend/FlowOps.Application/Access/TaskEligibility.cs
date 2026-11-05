using System.Diagnostics;
using System.Linq.Expressions;
using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Access;

/// <summary>
/// The one implementation of "who may decide a task" (AUTHORIZATION.md §4), used for decisions, for
/// <c>availableActions</c> and for the inbox. User U may decide task T iff T is assigned to U or to a role U holds
/// (T1/T2), U is active, and U is not the requester (I2). Whether T is still open and current is the domain's
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
            .Where(r => isActive && r.RequesterId != userId)
            .SelectMany(r => r.Tasks.AsQueryable().Where(t => t.Status == WorkTaskStatus.Open).Where(AssignedTo(me)),
                (r, t) => new DecidableTask { Request = r, Task = t });
    }

    /// <summary>Why <paramref name="me"/> may not decide <paramref name="task"/> of a request by <paramref name="requesterId"/>, or <c>null</c>.</summary>
    public static string? FailureCode(Guid requesterId, WorkTask task, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(me);
        if (!me.IsActive || !me.IsAssigned(task))
        {
            return ErrorCodes.TaskNotEligible;
        }

        // Checked after assignment, so only someone who would otherwise be eligible learns it's their own request.
        return requesterId == me.UserId ? ErrorCodes.TaskSelfDecision : null;
    }

    /// <summary>Throws 403 with <c>task.not_eligible</c> or <c>task.self_decision</c>. Call after the visibility check.</summary>
    public static void EnsureCanDecide(Guid requesterId, WorkTask task, AccessContext me)
    {
        var code = FailureCode(requesterId, task, me);
        if (code is not null)
        {
            throw new ForbiddenException(code, code == ErrorCodes.TaskSelfDecision
                ? "You cannot decide a task on your own request."
                : "You are not allowed to decide this task.");
        }
    }

    /// <summary>
    /// The caller's buttons for a request's open task, or <c>null</c> when they have none. Takes only what a
    /// projection can supply (requester id and the open task), so read paths needn't load the aggregate.
    /// </summary>
    public static AvailableActionsDto? AvailableActions(Guid requesterId, WorkTask? openTask, AccessContext me) =>
        openTask is { IsOpen: true } && FailureCode(requesterId, openTask, me) is null
            ? new AvailableActionsDto(openTask.Id, [.. openTask.AllowedActions.Select(ToDto)], VersionToken.Format(openTask.Version))
            : null;

    private static TaskAction ToDto(TaskDecisionAction action) => action switch
    {
        TaskDecisionAction.Approve => TaskAction.Approve,
        TaskDecisionAction.Reject => TaskAction.Reject,
        TaskDecisionAction.Fulfill => TaskAction.Fulfill,
        _ => throw new UnreachableException($"Unknown decision action {action}."),
    };
}

/// <summary>An open task the caller may decide, with its request; project both in one query.</summary>
public sealed class DecidableTask
{
    public required Request Request { get; init; }

    public required WorkTask Task { get; init; }
}
