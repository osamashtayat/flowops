using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Workflows;

/// <summary>DOMAIN_MODEL.md §7.2, TESTING.md §3.7: every operator on both sides of its boundary; values compare exactly.</summary>
public sealed class ConditionTests
{
    private static RequestFacts Amount(decimal amount) => new(Money.Create(amount, SupportedCurrencies.Jod), null, null);

    private static RequestFacts Leave(LeaveType type, int days) => new(null, type, days);

    [Theory]
    [InlineData(ConditionOperator.GreaterThan, 1000.000, false)]
    [InlineData(ConditionOperator.GreaterThan, 1000.001, true)]
    [InlineData(ConditionOperator.GreaterThanOrEqual, 999.999, false)]
    [InlineData(ConditionOperator.GreaterThanOrEqual, 1000.000, true)]
    [InlineData(ConditionOperator.LessThan, 1000.000, false)]
    [InlineData(ConditionOperator.LessThan, 999.999, true)]
    [InlineData(ConditionOperator.LessThanOrEqual, 1000.001, false)]
    [InlineData(ConditionOperator.LessThanOrEqual, 1000.000, true)]
    [InlineData(ConditionOperator.Equal, 1000.001, false)]
    [InlineData(ConditionOperator.Equal, 1000.000, true)]
    public void Amount_ComparesExactlyAgainst1000(ConditionOperator op, double amount, bool expected) =>
        Condition.Create(FactKey.Amount, op, ["1000.000"]).IsMetBy(Amount((decimal)amount)).ShouldBe(expected);

    [Theory]
    [InlineData(ConditionOperator.GreaterThan, 5, false)]
    [InlineData(ConditionOperator.GreaterThan, 6, true)]
    [InlineData(ConditionOperator.GreaterThanOrEqual, 4, false)]
    [InlineData(ConditionOperator.GreaterThanOrEqual, 5, true)]
    [InlineData(ConditionOperator.LessThan, 5, false)]
    [InlineData(ConditionOperator.LessThan, 4, true)]
    [InlineData(ConditionOperator.LessThanOrEqual, 6, false)]
    [InlineData(ConditionOperator.LessThanOrEqual, 5, true)]
    [InlineData(ConditionOperator.Equal, 6, false)]
    [InlineData(ConditionOperator.Equal, 5, true)]
    public void Days_ComparesAgainst5(ConditionOperator op, int days, bool expected) =>
        Condition.Create(FactKey.Days, op, ["5"]).IsMetBy(Leave(LeaveType.Annual, days)).ShouldBe(expected);

    [Theory]
    [InlineData(LeaveType.Sick, true)]
    [InlineData(LeaveType.Annual, false)]
    public void LeaveType_Is(LeaveType type, bool expected) =>
        Condition.Create(FactKey.LeaveType, ConditionOperator.Is, ["Sick"]).IsMetBy(Leave(type, 1)).ShouldBe(expected);

    [Theory]
    [InlineData(LeaveType.Sick, true)]
    [InlineData(LeaveType.Unpaid, true)]
    [InlineData(LeaveType.Annual, false)]
    public void LeaveType_IsOneOf(LeaveType type, bool expected) =>
        Condition.Create(FactKey.LeaveType, ConditionOperator.IsOneOf, ["Sick", "Unpaid"]).IsMetBy(Leave(type, 1)).ShouldBe(expected);

    [Theory]
    [InlineData(FactKey.Amount, ConditionOperator.Is)]
    [InlineData(FactKey.Days, ConditionOperator.IsOneOf)]
    [InlineData(FactKey.LeaveType, ConditionOperator.GreaterThan)]
    [InlineData(FactKey.Amount, ConditionOperator.Before)]
    public void Operator_MustSuitTheFactsType(FactKey fact, ConditionOperator op) =>
        ShouldFailWith(() => Condition.Create(fact, op, ["1"]), ErrorCodes.Validation);

    [Theory]
    [InlineData(FactKey.Amount, "1000.0001")]
    [InlineData(FactKey.Amount, "abc")]
    [InlineData(FactKey.Amount, "-5")]
    [InlineData(FactKey.Days, "2.5")]
    [InlineData(FactKey.LeaveType, "sick")]
    [InlineData(FactKey.LeaveType, "1")]
    public void Values_MustParseForTheFact(FactKey fact, string value)
    {
        var op = fact == FactKey.LeaveType ? ConditionOperator.Is : ConditionOperator.Equal;
        ShouldFailWith(() => Condition.Create(fact, op, [value]), ErrorCodes.Validation);
    }

    [Fact]
    public void Values_ExactlyOneExceptIsOneOf()
    {
        ShouldFailWith(() => Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, []), ErrorCodes.Validation);
        ShouldFailWith(() => Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1", "2"]), ErrorCodes.Validation);
        ShouldFailWith(() => Condition.Create(FactKey.LeaveType, ConditionOperator.IsOneOf, []), ErrorCodes.Validation);
    }

    [Fact]
    public void Money_IsKeptWithThreeDecimals() =>
        Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1000"]).Values.ShouldBe(["1000.000"]);

    [Fact]
    public void AFactOfAnotherRequestType_IsAProgrammingError() =>
        Should.Throw<InvalidOperationException>(() =>
            Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1000"]).IsMetBy(Leave(LeaveType.Sick, 2)));

    [Theory]
    [InlineData(FactKey.Amount, ConditionOperator.GreaterThan, "1000", "Only if amount is greater than 1,000.000 JOD")]
    [InlineData(FactKey.Days, ConditionOperator.GreaterThanOrEqual, "5", "Only if days is at least 5")]
    [InlineData(FactKey.LeaveType, ConditionOperator.IsOneOf, "Sick,Unpaid", "Only if leave type is one of Sick, Unpaid")]
    public void Describe_SaysTheConditionInWords(FactKey fact, ConditionOperator op, string values, string expected) =>
        Condition.Create(fact, op, values.Split(',')).Describe().ShouldBe(expected);
}
