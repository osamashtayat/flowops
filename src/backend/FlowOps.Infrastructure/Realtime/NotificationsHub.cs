using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FlowOps.Infrastructure.Realtime;

/// <summary>
/// <c>/hubs/notifications</c> (API_CONTRACT.md §9): server → client only, cookie-authenticated, same origin. SignalR's
/// default user id is the NameIdentifier claim (the user's id), so each user's connections form their own group. It has
/// no methods: clients never invoke anything in Phase 1.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
}
