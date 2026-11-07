namespace FlowOps.Application.Common;

/// <summary>
/// Live hints to connected users (API_CONTRACT.md §9, ADR-0009): ids only, never data. Implemented over SignalR in
/// Infrastructure. Use-case services don't call it directly: they go through <see cref="PostCommitPublisher"/>, which runs
/// after the commit and keeps a publish failure from failing the request.
/// </summary>
public interface IRealtimePublisher
{
    /// <summary><c>notificationCreated { notificationId, requestId }</c> to the notification's recipient.</summary>
    Task NotificationCreatedAsync(Guid recipientId, Guid notificationId, Guid? requestId, CancellationToken ct);

    /// <summary><c>requestChanged { requestId }</c> to each of <paramref name="recipients"/>.</summary>
    Task RequestChangedAsync(IReadOnlyCollection<Guid> recipients, Guid requestId, CancellationToken ct);

    /// <summary><c>tasksChanged {}</c> to each of <paramref name="recipients"/>.</summary>
    Task TasksChangedAsync(IReadOnlyCollection<Guid> recipients, CancellationToken ct);
}
