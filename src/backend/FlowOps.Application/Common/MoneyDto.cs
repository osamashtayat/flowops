using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Application.Common;

/// <summary>
/// Money on the wire: the amount is a decimal <b>string</b> so JavaScript never rounds it (API_CONTRACT.md §1).
/// The pattern checks the shape; <see cref="Money.Create"/> enforces the range and currency rules.
/// </summary>
public sealed record MoneyDto
{
    /// <summary>Up to 7 integer digits and 3 decimals, no sign, no exponent.</summary>
    public const string AmountPattern = @"^(0|[1-9][0-9]{0,6})(\.[0-9]{1,3})?$";

    [Required]
    [RegularExpression(AmountPattern)]
    public string Amount { get; init; } = "";

    [Required]
    [AllowedValues(SupportedCurrencies.Jod)]
    public string Currency { get; init; } = "";

    /// <summary>The wire form: always <see cref="Money.Scale"/> decimals, e.g. <c>"1250.500"</c>.</summary>
    public static MoneyDto From(Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new MoneyDto { Amount = money.Amount.ToString("0.000", CultureInfo.InvariantCulture), Currency = money.Currency };
    }

    /// <summary>Builds the domain value; a rule failure names the field under <paramref name="fieldPath"/> (e.g. <c>estimatedCost.amount</c>).</summary>
    public Money ToMoney(string fieldPath)
    {
        try
        {
            return Money.Create(decimal.Parse(Amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture), Currency);
        }
        catch (DomainException e) when (e.Field is not null)
        {
            throw new DomainException(e.Code, e.Message, $"{fieldPath}.{e.Field}");
        }
    }
}
