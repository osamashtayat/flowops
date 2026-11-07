using FlowOps.Application.Common;
using Microsoft.AspNetCore.SignalR;

namespace FlowOps.Infrastructure.Realtime;

/// <summary>The SignalR adapter for <see cref="IRealtimePublisher"/>. Event names and payload shapes are the contract (§9).</summary>
public sealed class SignalRRealtimePublisher(IHubContext<NotificationsHub> hub) : IRealtimePublisher
{
    public const string NotificationCreated = "notificationCreated";
    public const string RequestChanged = "requestChanged";
    public const string TasksChanged = "tasksChanged";

    public Task NotificationCreatedAsync(Guid recipientId, Guid notificationId, Guid? requestId, CancellationToken ct) =>
        hub.Clients.User(recipientId.ToString()).SendAsync(NotificationCreated, new NotificationCreatedPayload(notificationId, requestId), ct);

    public Task RequestChangedAsync(IReadOnlyCollection<Guid> recipients, Guid requestId, CancellationToken ct) =>
        recipients.Count == 0 ? Task.CompletedTask : hub.Clients.Users(Ids(recipients)).SendAsync(RequestChanged, new RequestChangedPayload(requestId), ct);

    public Task TasksChangedAsync(IReadOnlyCollection<Guid> recipients, CancellationToken ct) =>
        recipients.Count == 0 ? Task.CompletedTask : hub.Clients.Users(Ids(recipients)).SendAsync(TasksChanged, new TasksChangedPayload(), ct);

    private static string[] Ids(IEnumerable<Guid> users) => [.. users.Distinct().Select(u => u.ToString())];

    private sealed record NotificationCreatedPayload(Guid NotificationId, Guid? RequestId);

    private sealed record RequestChangedPayload(Guid RequestId);

    private sealed record TasksChangedPayload;
}
