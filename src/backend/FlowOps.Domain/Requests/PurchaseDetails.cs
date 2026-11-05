using FlowOps.Domain.Common;

namespace FlowOps.Domain.Requests;

/// <summary>Equipment purchase specifics (DOMAIN_MODEL.md §2.3).</summary>
public sealed class PurchaseDetails
{
    /// <summary>For persistence materialization only; state is set by the store, never by callers.</summary>
    private PurchaseDetails()
    {
        ItemName = "";
        Description = "";
        EstimatedCost = null!;
        Justification = "";
    }

    private PurchaseDetails(string itemName, string description, Money estimatedCost, string justification)
    {
        ItemName = itemName;
        Description = description;
        EstimatedCost = estimatedCost;
        Justification = justification;
    }

    public string ItemName { get; private set; }

    public string Description { get; private set; }

    public Money EstimatedCost { get; private set; }

    public string Justification { get; private set; }

    public static PurchaseDetails Create(string itemName, string description, Money estimatedCost, string justification)
    {
        ArgumentNullException.ThrowIfNull(estimatedCost);

        return new PurchaseDetails(
            Text.Required(itemName, RequestLimits.ItemNameMaxLength, "itemName"),
            Text.Required(description, RequestLimits.DescriptionMaxLength, "description"),
            estimatedCost,
            Text.Required(justification, RequestLimits.JustificationMaxLength, "justification"));
    }
}
