namespace FlowOps.Domain.Requests;

/// <summary>Append-only audit entry on a request (I9). Never updated or deleted.</summary>
public sealed class RequestActivity
{
    private RequestActivity(
        Guid requestId, ActivityType type, Guid? actorId, Guid? taskId, RequestStage? stage, string? body, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        RequestId = requestId;
        Type = type;
        ActorId = actorId;
        TaskId = taskId;
        Stage = stage;
        Body = body;
        OccurredAt = now;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public ActivityType Type { get; private set; }

    /// <summary><c>null</c> for system entries (<see cref="ActivityType.TaskAssigned"/>, <see cref="ActivityType.Completed"/>).</summary>
    public Guid? ActorId { get; private set; }

    public Guid? TaskId { get; private set; }

    /// <summary>The step the entry relates to; <c>null</c> for comments.</summary>
    public RequestStage? Stage { get; private set; }

    /// <summary>Comment text or rejection reason.</summary>
    public string? Body { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static RequestActivity Create(
        Guid requestId, ActivityType type, Guid? actorId, Guid? taskId, RequestStage? stage, string? body, DateTimeOffset now) =>
        new(requestId, type, actorId, taskId, stage, body, now);
}
