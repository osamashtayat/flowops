using FlowOps.Application.Requests;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
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

    /// <summary>
    /// A comment on a request. Recipients are chosen by the caller (WORKFLOWS.md §1.4). The text itself is not repeated:
    /// recipients read it on the request, where visibility rules apply.
    /// </summary>
    public static IEnumerable<Notification> CommentAdded(
        Request request, RequestActivity comment, string authorName, IEnumerable<Guid> recipients, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(recipients);
        var title = $"New comment on {request.Number}";
        var body = $"{authorName} commented on {Title(request)}.";
        return recipients.Where(r => r != comment.ActorId).Distinct().Select(recipient =>
            Notification.Create(recipient, NotificationType.CommentAdded, title, body, request.Id, taskId: null, now));
    }

    /// <summary>
    /// Tells the requester what a decision did (WORKFLOWS.md §1.4): rejected (with the decider's reason, which is not the
    /// leave reason), completed, approved (every approval granted, only fulfilment left; no process step is named) or one step approved.
    /// </summary>
    public static Notification DecisionOutcome(Request request, DecisionResult result, string deciderName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        var what = $"your {TypeLabel(request)}: {Title(request)}";
        var (type, title, body) = result switch
        {
            { DecidedTask.Outcome: WorkTaskOutcome.Rejected } =>
                (NotificationType.RequestRejected, $"{request.Number} was rejected",
                    $"{deciderName} rejected {what}. Reason: {result.DecidedTask.Comment}"),
            { Status: RequestStatus.Completed } =>
                (NotificationType.RequestCompleted, $"{request.Number} is complete", $"{deciderName} completed {what}."),
            { Status: RequestStatus.Approved } =>
                (NotificationType.RequestApproved, $"{request.Number} was approved", $"{deciderName} approved {what}. It is now waiting to be fulfilled."),
            _ =>
                (NotificationType.StepApproved, $"{request.Number}: a step was approved", $"{deciderName} approved a step of {what}."),
        };
        return Notification.Create(request.RequesterId, type, title, Truncate(body), request.Id, result.DecidedTask.Id, now);
    }

    /// <summary>A rejection reason can be up to 2000 characters; the notification body holds 500.</summary>
    private static string Truncate(string body) =>
        body.Length <= Notification.BodyMaxLength ? body : string.Concat(body.AsSpan(0, Notification.BodyMaxLength - 1), "…");

    /// <summary>An administrator's own role change waiting for one of <paramref name="recipients"/> (ADR-0012 §7).</summary>
    public static IEnumerable<Notification> RoleChangePending(
        RoleChange change, string proposerName, IEnumerable<Guid> recipients, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(recipients);
        const string title = "A role change is waiting for approval";
        var body = $"{proposerName} asked to change their own roles. Another administrator has to approve or reject it.";
        return recipients.Where(r => r != change.UserId).Distinct().Select(recipient =>
            Notification.Create(recipient, NotificationType.RoleChangePending, title, body, requestId: null, taskId: null, now));
    }

    /// <summary>Tells the proposer how their role change was decided. Never carries the rejection reason.</summary>
    public static Notification RoleChangeDecided(RoleChange change, string deciderName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(change);
        var (type, verb) = change.Status switch
        {
            RoleChangeStatus.Approved => (NotificationType.RoleChangeApproved, "approved"),
            RoleChangeStatus.Rejected => (NotificationType.RoleChangeRejected, "rejected"),
            var status => throw new InvalidOperationException($"A {status} role change is not a decision."),
        };
        return Notification.Create(change.UserId, type, $"Your role change was {verb}", $"{deciderName} {verb} your role change.",
            requestId: null, taskId: null, now);
    }

    private static string TypeLabel(Request request) => RequestTitle.TypeLabel(request.Type);

    private static string Title(Request request) => RequestTitle.For(request.Purchase, request.Leave);
}
