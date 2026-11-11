using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// The route shown on request details (ADR-0014 Q5, API_CONTRACT.md §4): one entry per task, in order, then the steps
/// still to come. Step order comes from <see cref="FixedProcesses"/>; P2-BE-05 switches this to the pinned workflow
/// version and adds skipped steps. Never saves.
/// </summary>
public static class RequestRoute
{
    /// <param name="tasks">The request's tasks in creation order.</param>
    public static IReadOnlyList<RouteStepDto> For(RequestType type, IEnumerable<WorkTask> tasks, RequestStatus status)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var route = tasks
            .Select(t => new RouteStepDto(StepRef.ForTask(t.Stage), t.Kind, t.IsOpen ? RouteStepState.Current : RouteStepState.Done))
            .ToList();
        if (status is RequestStatus.Completed or RequestStatus.Rejected || route.Count == 0)
        {
            return route;
        }

        var steps = FixedProcesses.For(type);
        var current = route[^1].Step;
        var remaining = steps.SkipWhile(s => StepRef.ForTask(s.Stage) != current).Skip(1);
        route.AddRange(remaining.Select(s => new RouteStepDto(StepRef.ForTask(s.Stage), s.Kind, RouteStepState.Planned)));
        return route;
    }
}
