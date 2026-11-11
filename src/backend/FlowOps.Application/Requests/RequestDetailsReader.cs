using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Requests;

/// <summary>
/// Builds <see cref="RequestDetailsDto"/> for the caller: one projection query through <see cref="AccessRules.VisibleTo"/>,
/// plus the §5 reason check. Shared by submission (BE-10) and <c>GET /api/requests/{id}</c> (BE-11). Never saves.
/// </summary>
public sealed class RequestDetailsReader(IAppDbContext db, AccessRules access)
{
    /// <summary>The request as <paramref name="me"/> may see it, or <c>null</c> when it doesn't exist or isn't visible.</summary>
    public async Task<RequestDetailsDto?> ReadAsync(Guid requestId, AccessContext me, CancellationToken ct)
    {
        var row = await access.VisibleTo(db.Requests.AsNoTracking(), me)
            .Where(r => r.Id == requestId)
            .Select(r => new DetailsRow
            {
                Id = r.Id,
                Number = r.Number,
                Type = r.Type,
                RequesterId = r.RequesterId,
                Requester = db.Users.Where(u => u.Id == r.RequesterId).Select(u => new UserRef(u.Id, u.DisplayName)).Single(),
                Department = db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => new DepartmentRef(d.Id, d.Name)).Single(),
                Status = r.Status,
                Stage = r.Stage,
                SubmittedAt = r.SubmittedAt,
                ClosedAt = r.ClosedAt,
                Purchase = r.Purchase,
                Leave = r.Leave,
                Tasks = r.Tasks.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).Select(t => new TaskRow
                {
                    Task = t,
                    AssigneeUser = db.Users.Where(u => u.Id == t.AssigneeUserId).Select(u => new UserRef(u.Id, u.DisplayName)).FirstOrDefault(),
                    DecidedBy = db.Users.Where(u => u.Id == t.DecidedById).Select(u => new UserRef(u.Id, u.DisplayName)).FirstOrDefault(),
                }).ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var reasonVisible = row.Leave is not null && await access.CanReadLeaveReasonAsync(requestId, me, ct);
        var openTask = row.Tasks.Select(t => t.Task).SingleOrDefault(t => t.IsOpen);
        return new RequestDetailsDto(
            row.Id,
            row.Number.Value,
            row.Type,
            RequestTitle.For(row.Purchase, row.Leave),
            row.Requester,
            row.Department,
            row.Status,
            row.Stage,
            row.SubmittedAt,
            row.ClosedAt,
            row.Purchase is { } purchase ? ToDto(purchase) : null,
            row.Leave is { } leave ? ToDto(leave, reasonVisible) : null,
            [.. row.Tasks.Select(ToDto)],
            TaskEligibility.AvailableActions(row.RequesterId, row.Tasks.Select(t => t.Task.DecidedById), openTask, me),
            // Commenting is open to every participant who can see the request (AUTHORIZATION.md §3), closed or not.
            CanComment: true,
            RequestRoute.For(row.Type, row.Tasks.Select(t => t.Task), row.Status));
    }

    private static PurchaseDetailsDto ToDto(PurchaseDetails p) => new(
        p.ItemName, p.Description, MoneyDto.From(p.EstimatedCost), p.Justification);

    private static LeaveDetailsDto ToDto(LeaveDetails l, bool reasonVisible) => new(
        l.StartDate, l.EndDate, l.Days, l.LeaveType, reasonVisible ? l.Reason : null, ReasonHidden: !reasonVisible);

    private static WorkTaskDto ToDto(TaskRow row)
    {
        var t = row.Task;
        return new WorkTaskDto(
            t.Id, t.Kind, t.Stage, t.Status,
            new TaskAssigneeDto(row.AssigneeUser, t.AssigneeRole is { } role ? Enum.Parse<Role>(role) : null),
            t.Outcome, row.DecidedBy, t.DecidedAt, t.Comment, t.CreatedAt);
    }

    private sealed class DetailsRow
    {
        public Guid Id { get; init; }
        public required RequestNumber Number { get; init; }
        public RequestType Type { get; init; }
        public Guid RequesterId { get; init; }
        public required UserRef Requester { get; init; }
        public required DepartmentRef Department { get; init; }
        public RequestStatus Status { get; init; }
        public RequestStage Stage { get; init; }
        public DateTimeOffset SubmittedAt { get; init; }
        public DateTimeOffset? ClosedAt { get; init; }
        public PurchaseDetails? Purchase { get; init; }
        public LeaveDetails? Leave { get; init; }
        public required List<TaskRow> Tasks { get; init; }
    }

    private sealed class TaskRow
    {
        public required WorkTask Task { get; init; }
        public UserRef? AssigneeUser { get; init; }
        public UserRef? DecidedBy { get; init; }
    }
}
