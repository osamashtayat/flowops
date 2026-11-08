using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowOps.Infrastructure.Realtime;

/// <summary>
/// <c>/hubs/notifications</c> (API_CONTRACT.md §9): server → client only, cookie-authenticated, same origin. SignalR's
/// default user id is the NameIdentifier claim (the user's id), so each user's connections form their own group. It has
/// no methods: clients never invoke anything in Phase 1. Connections are tracked so ending a user's sessions closes them,
/// and capped per user (<see cref="RealtimeConnectionOptions.MaxConnectionsPerUser"/>, #55).
/// </summary>
[Authorize]
public sealed partial class NotificationsHub(
    HubConnectionRegistry connections, IOptions<RealtimeConnectionOptions> options, ILogger<NotificationsHub> logger) : Hub
{
    public const string Path = "/hubs/notifications";

    /// <summary>
    /// Over the cap, throwing makes SignalR close the new connection with an error and no reconnect; it is never registered,
    /// so <see cref="OnDisconnectedAsync"/> (which SignalR then skips) has nothing to remove.
    /// </summary>
    public override Task OnConnectedAsync()
    {
        var max = options.Value.MaxConnectionsPerUser;
        if (!connections.TryAdd(Context, max))
        {
            LogRefused(logger, Context.UserIdentifier, max);
            throw new HubException("Too many open connections. Close another FlowOps tab and reload.");
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context);
        return base.OnDisconnectedAsync(exception);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hub connection of user {UserId} refused: already {Max} open")]
    private static partial void LogRefused(ILogger logger, string? userId, int max);
}
