using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// Projects requests to <see cref="RequestSummaryDto"/>: a translatable row first, then the title in memory
/// (<see cref="RequestTitle"/>). One home for the summary shape used by lists, the dashboard and the inbox. Never saves.
/// </summary>
public static class RequestSummaries
{
    /// <summary>Order and page the query first; this only selects columns.</summary>
    public static IQueryable<SummaryRow> Project(IAppDbContext db, IQueryable<Request> requests)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(requests);
        return requests.Select(r => new SummaryRow
        {
            Id = r.Id,
            Number = r.Number,
            Type = r.Type,
            ItemName = r.Purchase != null ? r.Purchase.ItemName : null,
            LeaveType = r.Leave != null ? r.Leave.LeaveType : null,
            LeaveStart = r.Leave != null ? r.Leave.StartDate : null,
            LeaveEnd = r.Leave != null ? r.Leave.EndDate : null,
            Requester = db.Users.Where(u => u.Id == r.RequesterId).Select(u => new UserRef(u.Id, u.DisplayName)).Single(),
            Department = db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => new DepartmentRef(d.Id, d.Name)).Single(),
            Status = r.Status,
            Stage = r.Stage,
            SubmittedAt = r.SubmittedAt,
            ClosedAt = r.ClosedAt,
        });
    }

    /// <summary>A projected request; <see cref="ToDto"/> adds the title.</summary>
    public sealed class SummaryRow
    {
        public Guid Id { get; init; }

        public required RequestNumber Number { get; init; }

        public RequestType Type { get; init; }

        public string? ItemName { get; init; }

        public LeaveType? LeaveType { get; init; }

        public DateOnly? LeaveStart { get; init; }

        public DateOnly? LeaveEnd { get; init; }

        public required UserRef Requester { get; init; }

        public required DepartmentRef Department { get; init; }

        public RequestStatus Status { get; init; }

        public RequestStage Stage { get; init; }

        public DateTimeOffset SubmittedAt { get; init; }

        public DateTimeOffset? ClosedAt { get; init; }

        public string Title => ItemName is not null
            ? RequestTitle.ForPurchase(ItemName)
            : RequestTitle.ForLeave(LeaveType!.Value, LeaveStart!.Value, LeaveEnd!.Value);

        public RequestSummaryDto ToDto() =>
            new(Id, Number.Value, Type, Title, Requester, Department, Status, Stage, SubmittedAt, ClosedAt);
    }
}
