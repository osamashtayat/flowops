using System.Text.Json.Serialization;

namespace FlowOps.Domain.Workflows;

/// <summary>
/// The facts catalogue (ADR-0014 §4, DOMAIN_MODEL.md §7.2): <c>amount</c> for purchases; <c>leaveType</c> and <c>days</c>
/// for leave. Served by name on the wire, hence the (BCL) serialization names.
/// </summary>
public enum FactKey
{
    [JsonStringEnumMemberName("amount")]
    Amount,

    [JsonStringEnumMemberName("leaveType")]
    LeaveType,

    [JsonStringEnumMemberName("days")]
    Days,
}

/// <summary>Operators by fact type (ADR-0014 §4). <c>Before</c>/<c>After</c> are for date facts; Phase 2a has none.</summary>
public enum ConditionOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    Is,
    IsOneOf,
    Before,
    After,
}

public enum FactValueType
{
    Money,
    Number,
    Enum,
    Date,
}
