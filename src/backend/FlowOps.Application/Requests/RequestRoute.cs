using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Requests;

/// <summary>
/// The route shown on request details (ADR-0014 Q5, API_CONTRACT.md §4): one entry per task, in order, then the steps
/// still to come from the request's pinned workflow version. P2-BE-05 adds conditions and skipped steps. Never saves.
/// </summary>
public static class RequestRoute
{
    /// <param name="pinnedSteps">The steps of the request's workflow version; only read while the request is open.</param>
    /// <param name="tasks">The request's tasks in creation order.</param>
    public static IReadOnlyList<RouteStepDto> For(IReadOnlyList<WorkflowStep> pinnedSteps, IEnumerable<WorkTask> tasks, RequestStatus status)
    {
        ArgumentNullException.ThrowIfNull(pinnedSteps);
        ArgumentNullException.ThrowIfNull(tasks);
        var route = tasks
            .Select(t => new RouteStepDto(StepRef.ForTask(t.Stage), t.Kind, t.IsOpen ? RouteStepState.Current : RouteStepState.Done))
            .ToList();
        if (status is RequestStatus.Completed or RequestStatus.Rejected || route.Count == 0)
        {
            return route;
        }

        var current = route[^1].Step.Key;
        var remaining = pinnedSteps.SkipWhile(s => s.Key != current).Skip(1);
        route.AddRange(remaining.Select(s => new RouteStepDto(StepRef.For(s), s.Kind, RouteStepState.Planned)));
        return route;
    }
}
