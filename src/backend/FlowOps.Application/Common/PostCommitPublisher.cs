using FlowOps.Application.Access;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowOps.Application.Common;

/// <summary>
/// What use-case services call after <c>SaveChangesAsync</c> succeeded (ADR-0009): works out who gets which hint and
/// sends it through <see cref="IRealtimePublisher"/>, without the request's cancellation token. The data is already committed, so a failure here (hub, network,
/// the recipient query) is logged and never turns a successful request into an error; clients refetch on reconnect.
/// </summary>
public sealed partial class PostCommitPublisher(
    IRealtimePublisher publisher, IAppDbContext db, AccessRules access, ILogger<PostCommitPublisher> logger)
{
    /// <summary>One <c>notificationCreated</c> per saved notification, to its recipient.</summary>
    public Task NotificationsAsync(IEnumerable<Notification> notifications) =>
        SafelyAsync("notificationCreated", async () =>
        {
            foreach (var n in notifications)
            {
                await publisher.NotificationCreatedAsync(n.RecipientId, n.Id, n.RequestId, Ct);
            }
        });

    /// <summary><c>requestChanged</c> to everyone who can currently see the request (<see cref="AccessRules.ViewerIds"/>).</summary>
    public Task RequestChangedAsync(Guid requestId) =>
        SafelyAsync("requestChanged", async () =>
        {
            var viewers = await access.ViewerIds(requestId).ToListAsync(Ct);
            await publisher.RequestChangedAsync(viewers, requestId, Ct);
        });

    /// <summary>
    /// <c>tasksChanged</c> to <paramref name="actors"/> (e.g. the decider, whose Done list changed) and to everyone who may
    /// decide any of <paramref name="tasks"/> (their Open list gained or lost it).
    /// </summary>
    public Task TasksChangedAsync(Request request, IEnumerable<WorkTask> tasks, IEnumerable<Guid> actors) =>
        SafelyAsync("tasksChanged", async () =>
        {
            var recipients = actors.ToHashSet();
            foreach (var task in tasks)
            {
                recipients.UnionWith(await TaskEligibility.EligibleDeciders(db, request, task).ToListAsync(Ct));
            }

            await publisher.TasksChangedAsync(recipients, Ct);
        });

    /// <summary>Closes <paramref name="userId"/>'s hub connections after their sessions were revoked.</summary>
    public Task SessionsEndedAsync(Guid userId) =>
        SafelyAsync("disconnect", async () => await publisher.DisconnectAsync(userId, Ct));

    // The commit has happened: a caller who hangs up now must not cancel other users' hints.
    private static CancellationToken Ct => CancellationToken.None;

    private async Task SafelyAsync(string eventName, Func<Task> publish)
    {
        try
        {
            await publish();
        }
#pragma warning disable CA1031 // Deliberately broad: after commit nothing may fail the request (ADR-0009).
        catch (Exception e)
#pragma warning restore CA1031
        {
            LogPublishFailed(logger, e, eventName);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime {EventName} could not be published after commit; clients will catch up on refetch")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string eventName);
}
