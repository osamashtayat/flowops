using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Requests;

/// <summary>
/// The route shown on request details and in the submit form's preview (ADR-0014 Q5, DOMAIN_MODEL.md §7.5). Never saves.
/// </summary>
public static class RequestRoute
{
    /// <summary>
    /// For a submitted request: each step it went through in version order (<c>Done</c>, or <c>Skipped</c> from its
    /// activity), the open step (<c>Current</c>), then the later steps whose conditions hold for its facts (<c>Planned</c>).
    /// A closed request shows only the steps it went through.
    /// </summary>
    /// <param name="pinned">The request's workflow version.</param>
    /// <param name="tasks">The request's tasks.</param>
    /// <param name="skipped">The steps of its <c>StepSkipped</c> activities.</param>
    public static IReadOnlyList<RouteStepDto> For(
        WorkflowVersion pinned, IEnumerable<WorkTask> tasks, IEnumerable<StepRef> skipped, RequestFacts facts)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(skipped);
        var steps = pinned.Steps;
        var taskList = tasks.ToList();
        // Steps run in version order and each at most once per request, so version order is history order.
        var route = taskList.Select(t => (t.Step, t.Kind, State: t.IsOpen ? RouteStepState.Current : RouteStepState.Done))
            .Concat(skipped.Select(s => (Step: s, KindOf(steps, s.Key), State: RouteStepState.Skipped)))
            .OrderBy(e => IndexOf(steps, e.Step.Key))
            .Select(e => new RouteStepDto(e.Step, e.Item2, e.State))
            .ToList();
        if (taskList.SingleOrDefault(t => t.IsOpen) is { } open)
        {
            route.AddRange(pinned.PlannedAfter(open.StepKey, facts).Select(s => new RouteStepDto(s.Ref, s.Kind, RouteStepState.Planned)));
        }

        return route;
    }

    /// <summary>Before submitting (<c>POST /api/requests/route-preview</c>): every step of the version, <c>Planned</c> or <c>Skipped</c>.</summary>
    public static IReadOnlyList<RouteStepDto> Preview(WorkflowVersion published, RequestFacts facts)
    {
        ArgumentNullException.ThrowIfNull(published);
        return [.. published.Steps.Select(s => new RouteStepDto(s.Ref, s.Kind, s.RunsFor(facts) ? RouteStepState.Planned : RouteStepState.Skipped))];
    }

    private static int IndexOf(IReadOnlyList<WorkflowStep> steps, string key)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Key == key)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Step '{key}' is not in the request's workflow version.");
    }

    private static WorkTaskKind KindOf(IReadOnlyList<WorkflowStep> steps, string key) =>
        steps.First(s => s.Key == key).Kind;
}
