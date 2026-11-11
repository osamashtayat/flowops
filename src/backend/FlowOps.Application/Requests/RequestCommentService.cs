using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Requests;

/// <summary>
/// <c>POST /api/requests/{id}/comments</c> (API_CONTRACT.md §4). Anyone who can see the request may comment, also once it
/// is closed (AUTHORIZATION.md §3); others get 404. Notifies the requester and the task participants except the author.
/// </summary>
public sealed class RequestCommentService(IAppDbContext db, AccessRules access, TimeProvider time, PostCommitPublisher realtime)
{
    public async Task<ActivityDto> AddAsync(Guid requestId, AddCommentRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var me = await access.ForCurrentUserAsync(ct);
        var request = await access.VisibleTo(db.Requests.Include(r => r.Tasks), me).SingleOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException();
        var author = await db.Users.Where(u => u.Id == me.UserId).Select(u => new UserRef(u.Id, u.DisplayName)).SingleAsync(ct);

        var now = time.GetUtcNow();
        var comment = request.AddComment(me.UserId, body.Body, now);
        var recipients = await RecipientsAsync(request, ct);
        var notifications = NotificationFactory.CommentAdded(request, comment, author.DisplayName, recipients, now).ToList();
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
        await realtime.NotificationsAsync(notifications);
        await realtime.RequestChangedAsync(request.Id);
        return new ActivityDto(comment.Id, comment.Type, author, comment.Step, comment.Body, comment.OccurredAt);
    }

    /// <summary>WORKFLOWS.md §1.4: the requester, everyone who decided a step, and whoever may decide the open task; active users only.</summary>
    private async Task<List<Guid>> RecipientsAsync(Domain.Requests.Request request, CancellationToken ct)
    {
        var candidates = request.Tasks.Where(t => t.DecidedById is not null).Select(t => t.DecidedById!.Value)
            .Append(request.RequesterId)
            .ToList();
        var active = await db.Users.Where(u => u.IsActive && candidates.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
        if (request.OpenTask is { } open)
        {
            active.AddRange(await TaskEligibility.EligibleDeciders(db, request, open).ToListAsync(ct));
        }

        return active;
    }
}
