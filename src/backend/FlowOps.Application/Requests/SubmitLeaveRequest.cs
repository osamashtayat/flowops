using System.ComponentModel.DataAnnotations;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// <c>POST /api/requests/leave</c>. Date rules (start ≥ today in the company time zone, end ≥ start,
/// ≤ 90 days) are enforced by <see cref="LeaveDetails.Create"/>.
/// </summary>
public sealed record SubmitLeaveRequest
{
    [Required]
    public DateOnly? StartDate { get; init; }

    [Required]
    public DateOnly? EndDate { get; init; }

    [Required]
    public LeaveType? LeaveType { get; init; }

    [StringLength(RequestLimits.LeaveReasonMaxLength)]
    public string? Reason { get; init; }
}
