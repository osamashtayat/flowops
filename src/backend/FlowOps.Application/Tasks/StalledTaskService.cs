using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using Microsoft.EntityFrameworkCore;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Tasks;

/// <summary>
/// <c>GET /api/admin/stalled-tasks</c> (API_CONTRACT.md §8): open tasks nobody can decide, oldest first. Admins see
/// metadata only (number, type, stage, assignee, age), never request details (AUTHORIZATION.md §4).
/// </summary>
public sealed class StalledTaskService(IAppDbContext db)
{
    public async Task<PagedResult<StalledTaskDto>> ListAsync(PageRequest query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await TaskEligibility.Stalled(db)
            .OrderBy(x => x.Task.CreatedAt).ThenBy(x => x.Task.Id)
            .Select(x => new
            {
                TaskId = x.Task.Id,
                RequestId = x.Request.Id,
                x.Request.Number,
                x.Request.Type,
                x.Task.StepKey,
                x.Task.StepName,
                AssigneeUser = db.Users.Where(u => u.Id == x.Task.AssigneeUserId).Select(u => new UserRef(u.Id, u.DisplayName)).FirstOrDefault(),
                x.Task.AssigneeRole,
                OpenSince = x.Task.CreatedAt,
            })
            .ToPagedResultAsync(query, ct);
        return page.Map(row => new StalledTaskDto(
            row.TaskId,
            row.RequestId,
            row.Number.Value,
            row.Type,
            new StepRef(row.StepKey, row.StepName),
            new TaskAssigneeDto(row.AssigneeUser, row.AssigneeRole is null ? null : Enum.Parse<Role>(row.AssigneeRole)),
            row.AssigneeRole is null ? StalledTaskReason.AssigneeInactive : StalledTaskReason.NoEligibleRoleMember,
            row.OpenSince));
    }
}
