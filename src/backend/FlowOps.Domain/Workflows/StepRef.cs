namespace FlowOps.Domain.Workflows;

/// <summary>
/// A process step as stored on requests, tasks and activities and shown by the API (ADR-0014 Q2, DOMAIN_MODEL.md §7.4):
/// the step's stable <see cref="Key"/> within its workflow version and a snapshot of its <see cref="Name"/>, so renaming
/// a step in a later version doesn't rewrite history.
/// </summary>
public sealed record StepRef(string Key, string Name)
{
    /// <summary>Rebuilds a stored snapshot; <c>null</c> when nothing was stored (a closed request, a comment).</summary>
    public static StepRef? From(string? key, string? name) => key is null ? null : new(key, name ?? key);
}
