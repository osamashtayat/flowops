using System.Globalization;
using System.Text.RegularExpressions;

namespace FlowOps.Domain.Requests;

/// <summary>Human-readable request number <c>FO-{yyyy}-{seq:000000}</c>; the sequence comes from the database.</summary>
public sealed partial record RequestNumber
{
    public const int MaxLength = 20;

    private RequestNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>Rebuilds a number from its stored or displayed form; rejects anything that isn't <c>FO-yyyy-nnnnnn</c>.</summary>
    public static RequestNumber Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Format().IsMatch(value) ? new RequestNumber(value) : throw new FormatException($"'{value}' is not a request number.");
    }

    public static RequestNumber Create(int year, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        return new RequestNumber(string.Create(CultureInfo.InvariantCulture, $"FO-{year:0000}-{sequence:000000}"));
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^FO-[0-9]{4}-[0-9]{6,13}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
