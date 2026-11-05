using System.ComponentModel.DataAnnotations;
using FlowOps.Domain.Common;

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
}
