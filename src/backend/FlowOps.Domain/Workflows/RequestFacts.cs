using FlowOps.Domain.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>
/// The facts a request's conditions are evaluated on (DOMAIN_MODEL.md §7.2). Read from the submitted details, which never
/// change, so a request always takes the same route. A fact its type doesn't have is null.
/// </summary>
public sealed record RequestFacts(Money? Amount, LeaveType? LeaveType, int? Days)
{
    public static RequestFacts Of(PurchaseDetails? purchase, LeaveDetails? leave) =>
        new(purchase?.EstimatedCost, leave?.LeaveType, leave?.Days);
}
