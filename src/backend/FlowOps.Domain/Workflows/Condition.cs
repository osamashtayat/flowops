using System.Globalization;
using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>
/// One typed comparison that decides whether a step runs (DOMAIN_MODEL.md §7.2): <see cref="Fact"/> <see cref="Operator"/>
/// <see cref="Values"/>. No <c>and</c>/<c>or</c>, nesting or free text. Values are kept in a canonical text form (money with
/// 3 decimals in JOD, a whole number, an enum name) and compared exactly: <c>1000.000</c> is not greater than <c>1000.000</c>.
/// </summary>
public sealed record Condition
{
    private Condition(FactKey fact, ConditionOperator op, IReadOnlyList<string> values)
    {
        Fact = fact;
        Operator = op;
        Values = values;
    }

    public FactKey Fact { get; }

    public ConditionOperator Operator { get; }

    public IReadOnlyList<string> Values { get; }

    /// <summary>Checks that the operator suits the fact's type and parses every value; throws <c>validation</c> otherwise.</summary>
    public static Condition Create(FactKey fact, ConditionOperator op, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var type = FactCatalogue.TypeOf(fact);
        if (!FactCatalogue.OperatorsFor(type).Contains(op))
        {
            throw DomainErrors.Validation($"'{op}' cannot be used with {FactCatalogue.LabelOf(fact)}.", "condition.operator");
        }

        var list = values.ToList();
        var countIsValid = op == ConditionOperator.IsOneOf ? list.Count >= 1 : list.Count == 1;
        if (!countIsValid)
        {
            throw DomainErrors.Validation(
                op == ConditionOperator.IsOneOf ? "Choose at least one value." : "A condition compares with exactly one value.",
                "condition.values");
        }

        return new Condition(fact, op, [.. list.Select(v => Canonical(type, v))]);
    }

    /// <summary>Whether a request with <paramref name="facts"/> takes the step. Pure; the fact must belong to the request's type.</summary>
    public bool IsMetBy(RequestFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return Fact switch
        {
            FactKey.Amount => Compare(Required(facts.Amount).Amount, ParseMoney(Values[0])),
            FactKey.Days => Compare(Required(facts.Days), int.Parse(Values[0], CultureInfo.InvariantCulture)),
            FactKey.LeaveType => Values.Contains(Required(facts.LeaveType).ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(facts), Fact, "Unknown fact."),
        };
    }

    /// <summary>The condition in words, for <c>StepSkipped</c> activities: "Only if amount is greater than 1,000.000 JOD".</summary>
    public string Describe()
    {
        var verb = Operator switch
        {
            ConditionOperator.GreaterThan => "is greater than",
            ConditionOperator.GreaterThanOrEqual => "is at least",
            ConditionOperator.LessThan => "is less than",
            ConditionOperator.LessThanOrEqual => "is at most",
            ConditionOperator.Equal or ConditionOperator.Is => "is",
            ConditionOperator.IsOneOf => "is one of",
            ConditionOperator.Before => "is before",
            ConditionOperator.After => "is after",
            _ => throw new InvalidOperationException($"Unknown operator {Operator}."),
        };
        return $"Only if {FactCatalogue.LabelOf(Fact)} {verb} {string.Join(", ", Values.Select(Display))}";
    }

    private bool Compare<T>(T actual, T expected)
        where T : IComparable<T>
    {
        var order = actual.CompareTo(expected);
        return Operator switch
        {
            ConditionOperator.GreaterThan => order > 0,
            ConditionOperator.GreaterThanOrEqual => order >= 0,
            ConditionOperator.LessThan => order < 0,
            ConditionOperator.LessThanOrEqual => order <= 0,
            ConditionOperator.Equal => order == 0,
            _ => throw new InvalidOperationException($"{Operator} does not compare numbers."),
        };
    }

    private T Required<T>(T? value)
        where T : notnull =>
        value ?? throw new InvalidOperationException($"The request has no '{Fact}' fact: the condition belongs to another request type.");

    private T Required<T>(T? value)
        where T : struct =>
        value ?? throw new InvalidOperationException($"The request has no '{Fact}' fact: the condition belongs to another request type.");

    private string Display(string value) => FactCatalogue.TypeOf(Fact) switch
    {
        FactValueType.Money => string.Create(CultureInfo.InvariantCulture, $"{ParseMoney(value):#,0.000} {SupportedCurrencies.Jod}"),
        _ => value,
    };

    private static string Canonical(FactValueType type, string? value) => type switch
    {
        FactValueType.Money => ParseMoney(value).ToString("0.000", CultureInfo.InvariantCulture),
        FactValueType.Number when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) =>
            n.ToString(CultureInfo.InvariantCulture),
        FactValueType.Enum when Enum.TryParse<LeaveType>(value, ignoreCase: false, out var e) && Enum.IsDefined(e) && value == e.ToString() =>
            e.ToString(),
        _ => throw DomainErrors.Validation($"'{value}' is not a valid value here.", "condition.values"),
    };

    private static decimal ParseMoney(string? value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            && decimal.Round(amount, Money.Scale) == amount && amount <= Money.MaxAmount
            ? amount
            : throw DomainErrors.Validation($"'{value}' is not an amount in JOD with at most {Money.Scale} decimals.", "condition.values");
}
