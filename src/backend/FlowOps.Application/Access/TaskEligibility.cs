using System.Linq.Expressions;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Access;

/// <summary>
/// The one implementation of "who may decide a task" (AUTHORIZATION.md §4), used for decisions, for
/// <c>availableActions</c> and for the inbox. User U may decide task T iff T is assigned to U or to a role U holds
/// (T1/T2), U is active, and U is not the requester (I2). Whether T is still open and current is the domain's
/// check (409s), so a late decider gets "already decided", not "not eligible".
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

    /// <summary>Open tasks <paramref name="me"/> may decide now (the inbox's "Open" list). One SQL query.</summary>
    public static IQueryable<WorkTask> DecidableBy(IQueryable<Request> requests, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(me);
        var userId = me.UserId;
        return requests
            .Where(r => me.IsActive && r.RequesterId != userId)
            .SelectMany(r => r.Tasks)
            .Where(t => t.Status == WorkTaskStatus.Open)
            .Where(AssignedTo(me));
    }

    /// <summary>Why <paramref name="me"/> may not decide <paramref name="task"/>, or <c>null</c> if they may.</summary>
    public static string? FailureCode(Request request, WorkTask task, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(me);
        if (!me.IsActive || !IsAssignedTo(task, me))
        {
            return ErrorCodes.TaskNotEligible;
        }

        // Checked after assignment, so only someone who would otherwise be eligible learns it's their own request.
        return request.RequesterId == me.UserId ? ErrorCodes.TaskSelfDecision : null;
    }

    /// <summary>Throws 403 with <c>task.not_eligible</c> or <c>task.self_decision</c>.</summary>
    public static void EnsureCanDecide(Request request, WorkTask task, AccessContext me)
    {
        var code = FailureCode(request, task, me);
        if (code is not null)
        {
            throw new ForbiddenException(code, code == ErrorCodes.TaskSelfDecision
                ? "You cannot decide a task on your own request."
                : "You are not allowed to decide this task.");
        }
    }

    /// <summary>The caller's buttons for <paramref name="request"/>'s open task, or <c>null</c> when they have none.</summary>
    public static AvailableActionsDto? AvailableActions(Request request, AccessContext me)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = request.OpenTask;
        if (task is null || task.Stage != request.Stage || FailureCode(request, task, me) is not null)
        {
            return null;
        }

        return new AvailableActionsDto(task.Id, [.. task.AllowedActions.Select(ToDto)], VersionToken.Format(task.Version));
    }

    /// <summary>The same expression as the queries use, compiled, so the in-memory and SQL checks cannot drift.</summary>
    private static bool IsAssignedTo(WorkTask task, AccessContext me) => AssignedTo(me).Compile()(task);

    private static TaskAction ToDto(TaskDecisionAction action) => action switch
    {
        TaskDecisionAction.Approve => TaskAction.Approve,
        TaskDecisionAction.Reject => TaskAction.Reject,
        _ => TaskAction.Fulfill,
    };
}
