namespace FlowOps.Domain.Notifications;

/// <summary>Kinds of persisted notification (DOMAIN_MODEL.md §2.7).</summary>
public enum NotificationType
{
    TaskAssigned,
    RequestApproved,
    RequestRejected,
    RequestCompleted,
    StepApproved,
    CommentAdded,
}
