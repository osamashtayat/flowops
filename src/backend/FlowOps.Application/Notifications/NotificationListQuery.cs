using FlowOps.Application.Common;

namespace FlowOps.Application.Notifications;

/// <summary><c>GET /api/notifications</c>: the caller's own notifications, newest first.</summary>
public sealed record NotificationListQuery : PageRequest
{
    public bool UnreadOnly { get; init; }
}
