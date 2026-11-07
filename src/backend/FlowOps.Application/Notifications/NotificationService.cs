using FlowOps.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Notifications;

/// <summary>
/// The caller's own notifications (API_CONTRACT.md §6). Every query is scoped to <see cref="ICurrentUser"/>, so another
/// user's notification is "not found" (404), never "forbidden". Text was rendered when the notification was created
/// (<see cref="NotificationFactory"/>), so nothing here can expose more than the recipient was meant to see.
/// </summary>
public sealed class NotificationService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var notifications = Mine().AsNoTracking();
        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => n.ReadAt == null);
        }

        return await notifications
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.RequestId, n.TaskId, n.CreatedAt, n.ReadAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<UnreadCountDto> UnreadCountAsync(CancellationToken ct) =>
        new(await Mine().CountAsync(n => n.ReadAt == null, ct));

    /// <summary>Idempotent: marking a read notification again keeps its first read time.</summary>
    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await Mine().SingleOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException();
        notification.MarkRead(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var unread = await Mine().Where(n => n.ReadAt == null).ToListAsync(ct);
        var now = time.GetUtcNow();
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Domain.Notifications.Notification> Mine()
    {
        var userId = currentUser.Id;
        return db.Notifications.Where(n => n.RecipientId == userId);
    }
}
