using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>
/// Which facts each request type has and which operators each fact type takes (DOMAIN_MODEL.md §7.2). The one home of
/// that knowledge: conditions, validation and the editor's catalogue all read it. Adding a fact is a code change.
/// </summary>
public static class FactCatalogue
{
    public static IReadOnlyList<FactKey> For(RequestType type) => type switch
    {
        RequestType.EquipmentPurchase => [FactKey.Amount],
        RequestType.Leave => [FactKey.LeaveType, FactKey.Days],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown request type."),
    };

    public static FactValueType TypeOf(FactKey fact) => fact switch
    {
        FactKey.Amount => FactValueType.Money,
        FactKey.LeaveType => FactValueType.Enum,
        FactKey.Days => FactValueType.Number,
        _ => throw new ArgumentOutOfRangeException(nameof(fact), fact, "Unknown fact."),
    };

    public static IReadOnlyList<ConditionOperator> OperatorsFor(FactValueType type) => type switch
    {
        FactValueType.Money or FactValueType.Number =>
        [
            ConditionOperator.GreaterThan, ConditionOperator.GreaterThanOrEqual, ConditionOperator.LessThan,
            ConditionOperator.LessThanOrEqual, ConditionOperator.Equal,
        ],
        FactValueType.Enum => [ConditionOperator.Is, ConditionOperator.IsOneOf],
        FactValueType.Date => [ConditionOperator.Before, ConditionOperator.After],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown fact type."),
    };

    /// <summary>How the fact reads in a sentence ("Only if <b>amount</b> is greater than …").</summary>
    public static string LabelOf(FactKey fact) => fact switch
    {
        FactKey.Amount => "amount",
        FactKey.LeaveType => "leave type",
        FactKey.Days => "days",
        _ => throw new ArgumentOutOfRangeException(nameof(fact), fact, "Unknown fact."),
    };
}
