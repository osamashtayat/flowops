using System.Globalization;

namespace FlowOps.Domain.Requests;

/// <summary>Human-readable request number <c>FO-{yyyy}-{seq:000000}</c>; the sequence comes from the database.</summary>
public sealed record RequestNumber
{
    private RequestNumber(string value) => Value = value;

    public string Value { get; }

    public static RequestNumber Create(int year, long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        return new RequestNumber(string.Create(CultureInfo.InvariantCulture, $"FO-{year:0000}-{sequence:000000}"));
    }

    public override string ToString() => Value;
}
