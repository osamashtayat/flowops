using FlowOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    HubConnectionRegistry connections, SignInManager<ApplicationUser> signIn, IOptions<RealtimeConnectionOptions> options,
    ILogger<NotificationsHub> logger) : Hub
{
    public const string Path = "/hubs/notifications";

    /// <summary>
    /// Over the cap, or when the session was already revoked, throwing makes SignalR close the new connection with an error
    /// and no reconnect. SignalR then skips <see cref="OnDisconnectedAsync"/>, so a refused connection is removed here.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var max = options.Value.MaxConnectionsPerUser;
        if (!connections.TryAdd(Context, max))
        {
            LogRefused(logger, Context.UserIdentifier, max);
            throw new HubException("Too many open connections. Close another FlowOps tab and reload.");
        }

        // After registering, so a session ended from now on is caught by AbortAll, and one ended before by this check (#78).
        if (await signIn.ValidateSecurityStampAsync(Context.User) is null)
        {
            connections.Remove(Context);
            LogRevokedSession(logger, Context.UserIdentifier);
            throw new HubException("Your session has ended. Please sign in again.");
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context);
        return base.OnDisconnectedAsync(exception);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hub connection of user {UserId} refused: already {Max} open")]
    private static partial void LogRefused(ILogger logger, string? userId, int max);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hub connection of user {UserId} refused: session revoked or user inactive")]
    private static partial void LogRevokedSession(ILogger logger, string? userId);
}
