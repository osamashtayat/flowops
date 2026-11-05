using System.ComponentModel.DataAnnotations;
using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// <c>POST /api/requests/purchase</c>. Requester, department and time come from the server (I14);
/// unknown properties such as <c>requesterId</c> are ignored.
/// </summary>
public sealed record SubmitPurchaseRequest
{
    [Required]
    [StringLength(RequestLimits.ItemNameMaxLength, MinimumLength = 1)]
    public string ItemName { get; init; } = "";

    [Required]
    [StringLength(RequestLimits.DescriptionMaxLength, MinimumLength = 1)]
    public string Description { get; init; } = "";

    [Required]
    public MoneyDto? EstimatedCost { get; init; }

    [Required]
    [StringLength(RequestLimits.JustificationMaxLength, MinimumLength = 1)]
    public string Justification { get; init; } = "";
}
