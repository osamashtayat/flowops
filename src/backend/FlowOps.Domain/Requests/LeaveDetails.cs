using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Requests;

/// <summary>Leave specifics (DOMAIN_MODEL.md §2.3). <see cref="Reason"/> is sensitive (AUTHORIZATION.md §5).</summary>
public sealed class LeaveDetails
{
    private LeaveDetails(DateOnly startDate, DateOnly endDate, LeaveType leaveType, string? reason)
    {
        StartDate = startDate;
        EndDate = endDate;
        LeaveType = leaveType;
        Reason = reason;
    }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public LeaveType LeaveType { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>Calendar days, counting both start and end day.</summary>
    public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;

    /// <summary>
    /// "Today" is the calendar date in the company time zone (default Asia/Amman), not server UTC,
    /// so the caller passes the current instant and the configured zone.
    /// </summary>
    public static LeaveDetails Create(
        DateOnly startDate, DateOnly endDate, LeaveType leaveType, string? reason, DateTimeOffset now, TimeZoneInfo companyTimeZone)
    {
        ArgumentNullException.ThrowIfNull(companyTimeZone);

        if (!Enum.IsDefined(leaveType))
        {
            throw DomainErrors.Validation("leaveType is not a known leave type.", "leaveType");
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, companyTimeZone).DateTime);
        if (startDate < today)
        {
            throw DomainErrors.Validation("startDate must not be in the past.", "startDate");
        }

        if (endDate < startDate)
        {
            throw DomainErrors.Validation("endDate must not be before startDate.", "endDate");
        }

        var details = new LeaveDetails(
            startDate, endDate, leaveType, Text.Optional(reason, RequestLimits.LeaveReasonMaxLength, "reason"));

        return details.Days <= RequestLimits.LeaveMaxDays
            ? details
            : throw DomainErrors.Validation($"Leave must not exceed {RequestLimits.LeaveMaxDays} days.", "endDate");
    }
}
