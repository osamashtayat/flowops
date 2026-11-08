using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using FlowOps.Application.Requests;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowOps.Application.Tasks;

/// <summary>
/// Approve, reject and fulfil (API_CONTRACT.md §5, WORKFLOWS.md §2–5): three thin entry points, one pipeline
/// (CODE_STANDARDS.md §2.4). Order: visibility (404) → eligibility (403) → version (409) → domain (409) → notifications →
/// one save. A lost race surfaces as 409 <c>concurrency.conflict</c> from <c>xmin</c>; it is never retried.
/// </summary>
public sealed partial class TaskDecisionService(
    IAppDbContext db, AccessRules access, TimeProvider time, PostCommitPublisher realtime, ILogger<TaskDecisionService> logger)
{
    public Task ApproveAsync(Guid taskId, ApproveTaskRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        return DecideAsync(taskId, body.Version, TaskDecision.Approve(body.Comment), ct);
    }

    public Task RejectAsync(Guid taskId, RejectTaskRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        return DecideAsync(taskId, body.Version, TaskDecision.Reject(body.Reason), ct);
    }

    public Task FulfillAsync(Guid taskId, FulfillTaskRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        return DecideAsync(taskId, body.Version, TaskDecision.Fulfill(body.Note), ct);
    }

    private async Task DecideAsync(Guid taskId, string version, TaskDecision decision, CancellationToken ct)
    {
        var me = await access.ForCurrentUserAsync(ct);
        var request = await access.VisibleTo(db.Requests.Include(r => r.Tasks), me)
            .SingleOrDefaultAsync(r => r.Tasks.Any(t => t.Id == taskId), ct) ?? throw new NotFoundException();
        var task = request.Tasks.Single(t => t.Id == taskId);
        TaskEligibility.EnsureCanDecide(request.RequesterId, task, me);
        // Only an open task can be stale; a decided one gets the domain's clearer "already decided".
        if (task.IsOpen && (!VersionToken.TryParse(version, out var seen) || seen != task.Version))
        {
            throw Conflict();
        }

        var requester = await TaskAssigneeResolver.LoadRequesterAsync(db, request.RequesterId, ct)
            ?? throw new InvalidOperationException("A request's requester always exists: users are never deleted.");
        var now = time.GetUtcNow();
        var result = request.DecideTask(taskId, me.UserId, decision, step => ResolveAssignee(step, requester), now);

        var notifications = await AddNotificationsAsync(request, result, requester, me.UserId, now, ct);
        await SaveAsync(ct);
        LogDecided(logger, me.UserId, task.Id, request.Id, result.DecidedTask.Outcome);

        // After commit only (ADR-0009): the decided task left its deciders' inboxes, the next one entered others'.
        await realtime.NotificationsAsync(notifications);
        await realtime.RequestChangedAsync(request.Id);
        await realtime.TasksChangedAsync(request, result.NextTask is { } next ? [task, next] : [task], [me.UserId]);
    }

    /// <summary>Phase 1 only assigns the first step to the manager, so a manager lookup here is not expected to fail.</summary>
    private static Assignee ResolveAssignee(ProcessStep step, TaskAssigneeResolver.Requester requester) =>
        TaskAssigneeResolver.Resolve(step, step.Assignee.IsRequesterManager ? TaskAssigneeResolver.RequireManager(requester) : Guid.Empty);

    /// <summary>
    /// WORKFLOWS.md §1.4: whoever may decide the next task, and the requester about the outcome, unless the requester is
    /// inactive (#56): like every other recipient rule, inactive users get no notifications.
    /// </summary>
    private async Task<List<Notification>> AddNotificationsAsync(
        Request request, DecisionResult result, TaskAssigneeResolver.Requester requester, Guid deciderId, DateTimeOffset now, CancellationToken ct)
    {
        var notifications = new List<Notification>();
        if (result.NextTask is { } next)
        {
            var deciders = await TaskEligibility.EligibleDeciders(db, next.AssigneeUserId, next.AssigneeRole, request.RequesterId).ToListAsync(ct);
            notifications.AddRange(NotificationFactory.TaskAssigned(request, next, requester.DisplayName, deciders, now));
        }

        if (requester.IsActive)
        {
            var deciderName = await db.Users.Where(u => u.Id == deciderId).Select(u => u.DisplayName).SingleAsync(ct);
            notifications.Add(NotificationFactory.DecisionOutcome(request, result, deciderName, now));
        }

        db.Notifications.AddRange(notifications);
        return notifications;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
    }

    private static ConflictException Conflict() =>
        new(ErrorCodes.ConcurrencyConflict, "Someone else changed this task. Reload to see the latest state.");

    // Ids and the outcome only: never the comment or reason.
    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} decided task {TaskId} of request {RequestId}: {Outcome}")]
    private static partial void LogDecided(ILogger logger, Guid actorId, Guid taskId, Guid requestId, WorkTaskOutcome? outcome);
}
