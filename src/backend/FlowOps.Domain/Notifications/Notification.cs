using FlowOps.Domain.Common;

namespace FlowOps.Domain.Notifications;

/// <summary>
/// A persisted message to one user (DOMAIN_MODEL.md §2.7). Title and body are rendered server-side
/// as plain text by <c>NotificationFactory</c>, which also guarantees they never contain a leave reason.
/// </summary>
public sealed class Notification
{
    public const int TitleMaxLength = 150;
    public const int BodyMaxLength = 500;

    /// <summary>For persistence materialization only; state is set by the store, never by callers.</summary>
    private Notification()
    {
        Title = "";
        Body = "";
    }

    private Notification(
        Guid recipientId, NotificationType type, string title, string body, Guid? requestId, Guid? taskId, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        RecipientId = recipientId;
        Type = type;
        Title = title;
        Body = body;
        RequestId = requestId;
        TaskId = taskId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid RecipientId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public Guid? RequestId { get; private set; }

    public Guid? TaskId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid recipientId, NotificationType type, string title, string body, Guid? requestId, Guid? taskId, DateTimeOffset now)
    {
        if (recipientId == Guid.Empty)
        {
            throw new ArgumentException("A notification needs a recipient.", nameof(recipientId));
        }

        return new Notification(
            recipientId,
            type,
            Text.Required(title, TitleMaxLength, "title"),
            Text.Required(body, BodyMaxLength, "body"),
            requestId,
            taskId,
            now);
    }

    /// <summary>Idempotent: the first read time is kept.</summary>
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
