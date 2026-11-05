using System.Diagnostics;
using System.Globalization;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// The one-line title of a request (API_CONTRACT.md §4): the item name for purchases, e.g. "Annual leave · 12–14 Nov"
/// for leave. Plain text, never the leave reason. Used by details, lists, the inbox and notifications.
/// </summary>
public static class RequestTitle
{
    public static string ForPurchase(string itemName) => itemName;

    public static string ForLeave(LeaveType leaveType, DateOnly start, DateOnly end) =>
        $"{LeaveLabel(leaveType)} · {DateRange(start, end)}";

    public static string LeaveLabel(LeaveType leaveType) => leaveType switch
    {
        LeaveType.Annual => "Annual leave",
        LeaveType.Sick => "Sick leave",
        LeaveType.Unpaid => "Unpaid leave",
        LeaveType.Other => "Other leave",
        _ => throw new UnreachableException($"Unknown leave type {leaveType}."),
    };

    /// <summary>"12 Nov", "12–14 Nov", "30 Nov – 2 Dec", or with years when they differ.</summary>
    public static string DateRange(DateOnly start, DateOnly end)
    {
        var c = CultureInfo.InvariantCulture;
        if (start == end)
        {
            return start.ToString("d MMM", c);
        }

        if (start.Year != end.Year)
        {
            return $"{start.ToString("d MMM yyyy", c)} – {end.ToString("d MMM yyyy", c)}";
        }

        return start.Month == end.Month
            ? $"{start.Day.ToString(c)}–{end.ToString("d MMM", c)}"
            : $"{start.ToString("d MMM", c)} – {end.ToString("d MMM", c)}";
    }
}
