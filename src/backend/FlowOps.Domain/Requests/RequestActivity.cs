using FlowOps.Domain.Common;
using FlowOps.Domain.Workflows;
namespace FlowOps.Domain.Requests;

/// <summary>Append-only audit entry on a request (I9). Never updated or deleted.</summary>
public sealed class RequestActivity
{
    /// <summary>For persistence materialization only; state is set by the store, never by callers.</summary>
    private RequestActivity()
    {
    }

    private RequestActivity(
        Guid requestId, ActivityType type, Guid? actorId, Guid? taskId, StepRef? step, string? body, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        RequestId = requestId;
        Type = type;
        ActorId = actorId;
        TaskId = taskId;
        StepKey = step?.Key;
        StepName = step?.Name;
        Body = body;
        OccurredAt = now;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public ActivityType Type { get; private set; }

    /// <summary><c>null</c> for system entries (<see cref="ActivityType.TaskAssigned"/>, <see cref="ActivityType.Completed"/>).</summary>
    public Guid? ActorId { get; private set; }

    public Guid? TaskId { get; private set; }

    /// <summary>Stored half of <see cref="Step"/>.</summary>
    public string? StepKey { get; private set; }

    /// <summary>Stored half of <see cref="Step"/>: the step's name when the entry was written.</summary>
    public string? StepName { get; private set; }

    /// <summary>The step the entry relates to; <c>null</c> for comments and for <see cref="ActivityType.Completed"/>.</summary>
    public StepRef? Step => StepRef.From(StepKey, StepName);

    /// <summary>Comment text or rejection reason.</summary>
    public string? Body { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static RequestActivity Create(
        Guid requestId, ActivityType type, Guid? actorId, Guid? taskId, StepRef? step, string? body, DateTimeOffset now) =>
        new(requestId, type, actorId, taskId, step, body, now);
}
