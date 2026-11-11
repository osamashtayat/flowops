using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Tasks;

/// <summary>
/// <c>GET /api/tasks</c> (API_CONTRACT.md §5). Open: tasks the caller may decide now (<see cref="TaskEligibility"/>), oldest
/// first. Done: tasks the caller decided, newest first. Both are visible by construction (R3), so no other request leaks.
/// </summary>
public sealed class TaskQueryService(IAppDbContext db, AccessRules access)
{
    public async Task<PagedResult<InboxItemDto>> ListAsync(TaskListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var me = await access.ForCurrentUserAsync(ct);
        var requests = db.Requests.AsNoTracking();
        var rows = query.Status == WorkTaskStatus.Open
            ? TaskEligibility.DecidableBy(requests, me)
                .OrderBy(x => x.Task.CreatedAt).ThenBy(x => x.Task.Id)
                .Select(x => new TaskRow(x.Task, x.Request.Id, x.Request.RequesterId, x.Request.Tasks.Select(t => t.DecidedById).ToList()))
            : DecidedBy(requests, me)
                .OrderByDescending(x => x.Task.DecidedAt).ThenByDescending(x => x.Task.Id)
                .Select(x => new TaskRow(x.Task, x.Request.Id, x.Request.RequesterId, x.Request.Tasks.Select(t => t.DecidedById).ToList()));

        var page = await rows.ToPagedResultAsync(query, ct);
        var requestIds = page.Items.Select(r => r.RequestId).Distinct().ToList();
        var summaries = await RequestSummaries.Project(db, requests.Where(r => requestIds.Contains(r.Id))).ToDictionaryAsync(s => s.Id, ct);
        return page.Map(row => ToDto(row, summaries[row.RequestId], me));
    }

    private static IQueryable<DecidableTask> DecidedBy(IQueryable<Request> requests, AccessContext me)
    {
        var userId = me.UserId;
        var isActive = me.IsActive;
        return requests
            .Where(r => isActive)
            .SelectMany(r => r.Tasks.Where(t => t.DecidedById == userId), (r, t) => new DecidableTask { Request = r, Task = t });
    }

    private static InboxItemDto ToDto(TaskRow row, RequestSummaries.SummaryRow summary, AccessContext me)
    {
        var task = row.Task;
        var actions = TaskEligibility.AvailableActions(row.RequesterId, row.DeciderIds, task, me)?.Actions ?? [];
        var request = new InboxRequestDto(
            summary.Id, summary.Number.Value, summary.Type, summary.Title, summary.Requester, summary.Department,
            summary.Status, summary.Step, summary.SubmittedAt, summary.ClosedAt, summary.KeyDetail);
        return new InboxItemDto(
            task.Id, task.Kind, task.Step, task.CreatedAt, VersionToken.Format(task.Version), task.Status, task.Outcome,
            task.DecidedAt, actions, request);
    }

    private sealed record TaskRow(WorkTask Task, Guid RequestId, Guid RequesterId, List<Guid?> DeciderIds);
}
