using FlowOps.Application.Requests;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Notifications;

/// <summary>
/// Who gets notified and what it says (ADR-0009): the single home of notification text. Plain text only, and never
/// a leave reason (AUTHORIZATION.md §5): leave notifications mention the number, type and dates. Never saves.
/// </summary>
public static class NotificationFactory
{
    /// <summary>A task waiting for each of <paramref name="recipients"/>, e.g. the manager after submission.</summary>
    public static IEnumerable<Notification> TaskAssigned(
        Request request, WorkTask task, string requesterName, IEnumerable<Guid> recipients, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(recipients);
        var title = $"{request.Number} is waiting for you";
        var body = $"{requesterName} submitted a {TypeLabel(request)}: {Title(request)}.";
        return recipients.Distinct().Select(recipient =>
            Notification.Create(recipient, NotificationType.TaskAssigned, title, body, request.Id, task.Id, now));
    }

    private static string TypeLabel(Request request) => request.Type == RequestType.Leave ? "leave request" : "purchase request";

    private static string Title(Request request) => request.Leave is { } leave
        ? RequestTitle.ForLeave(leave.LeaveType, leave.StartDate, leave.EndDate)
        : RequestTitle.ForPurchase(request.Purchase!.ItemName);
}
