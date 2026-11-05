using FlowOps.Domain.Notifications;

namespace FlowOps.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    Guid? RequestId,
    Guid? TaskId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record UnreadCountDto(int Count);
