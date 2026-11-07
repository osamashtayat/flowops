using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FlowOps.Infrastructure.Realtime;

/// <summary>
/// <c>/hubs/notifications</c> (API_CONTRACT.md §9): server → client only, cookie-authenticated, same origin. SignalR's
/// default user id is the NameIdentifier claim (the user's id), so each user's connections form their own group. It has
/// no methods: clients never invoke anything in Phase 1. Connections are tracked so ending a user's sessions closes them.
/// </summary>
[Authorize]
public sealed class NotificationsHub(HubConnectionRegistry connections) : Hub
{
    public const string Path = "/hubs/notifications";

    public override Task OnConnectedAsync()
    {
        connections.Add(Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context);
        return base.OnDisconnectedAsync(exception);
    }
}
