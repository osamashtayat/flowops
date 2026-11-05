namespace FlowOps.Domain.Requests;

/// <summary>
/// Input limits for requests, the single server-side home (CODE_STANDARDS.md §7).
/// Request DTO annotations reference these; money limits live on <see cref="Common.Money"/>.
/// </summary>
public static class RequestLimits
{
    public const int ItemNameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int JustificationMaxLength = 2000;
    public const int LeaveReasonMaxLength = 1000;

    /// <summary>Maximum leave span in calendar days, counting start and end day.</summary>
    public const int LeaveMaxDays = 90;

    /// <summary>Decision comments, rejection reasons, fulfillment notes and request comments.</summary>
    public const int CommentMaxLength = 2000;
}
