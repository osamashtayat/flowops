using System.Diagnostics;
using System.Globalization;
using FlowOps.Application.Common;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

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
            EstimatedAmount = r.Purchase != null ? r.Purchase.EstimatedCost.Amount : null,
            EstimatedCurrency = r.Purchase != null ? r.Purchase.EstimatedCost.Currency : null,
            LeaveType = r.Leave != null ? r.Leave.LeaveType : null,
            LeaveStart = r.Leave != null ? r.Leave.StartDate : null,
            LeaveEnd = r.Leave != null ? r.Leave.EndDate : null,
            Requester = db.Users.Where(u => u.Id == r.RequesterId).Select(u => new UserRef(u.Id, u.DisplayName)).Single(),
            Department = db.Departments.Where(d => d.Id == r.DepartmentId).Select(d => new DepartmentRef(d.Id, d.Name)).Single(),
            Status = r.Status,
            StepKey = r.StepKey,
            StepName = r.StepName,
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

        public decimal? EstimatedAmount { get; init; }

        public string? EstimatedCurrency { get; init; }

        public LeaveType? LeaveType { get; init; }

        public DateOnly? LeaveStart { get; init; }

        public DateOnly? LeaveEnd { get; init; }

        public required UserRef Requester { get; init; }

        public required DepartmentRef Department { get; init; }

        public RequestStatus Status { get; init; }

        public string? StepKey { get; init; }

        public string? StepName { get; init; }

        public StepRef? Step => StepRef.From(StepKey, StepName);

        public DateTimeOffset SubmittedAt { get; init; }

        public DateTimeOffset? ClosedAt { get; init; }

        // The row holds only the columns lists need (never the leave reason), so it dispatches on Type itself rather
        // than through RequestTitle.For(details); both switches throw for an unknown type instead of guessing (#84).
        public string Title => Type switch
        {
            RequestType.EquipmentPurchase => RequestTitle.ForPurchase(ItemName!),
            RequestType.Leave => RequestTitle.ForLeave(LeaveType!.Value, LeaveStart!.Value, LeaveEnd!.Value),
            _ => throw new UnreachableException($"Unknown request type {Type}."),
        };

        /// <summary>The inbox's one-line detail: <c>"1,250.500 JOD"</c> (the same order as the client's money format, #66) or <c>"3 days · Annual"</c> (API_CONTRACT.md §5).</summary>
        public string KeyDetail => Type switch
        {
            RequestType.EquipmentPurchase => string.Create(CultureInfo.InvariantCulture, $"{EstimatedAmount!.Value:#,0.000} {EstimatedCurrency}"),
            RequestType.Leave => string.Create(CultureInfo.InvariantCulture, $"{LeaveDays} {(LeaveDays == 1 ? "day" : "days")} · {LeaveType}"),
            _ => throw new UnreachableException($"Unknown request type {Type}."),
        };

        private int LeaveDays => LeaveDetails.DaysBetween(LeaveStart!.Value, LeaveEnd!.Value);

        public RequestSummaryDto ToDto() =>
            new(Id, Number.Value, Type, Title, Requester, Department, Status, Step, SubmittedAt, ClosedAt);
    }
}
