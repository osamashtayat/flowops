using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Common;

/// <summary>
/// A positive monetary amount in a supported currency. JOD has three minor-unit decimals,
/// hence <see cref="Scale"/> = 3. No arithmetic in Phase 1 by design.
/// </summary>
public sealed record Money
{
    public const int Scale = 3;
    public const decimal MaxAmount = 1_000_000.000m;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Create(decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (!SupportedCurrencies.IsSupported(currency))
        {
            throw DomainErrors.Validation($"Currency '{currency}' is not supported.", "currency");
        }

        if (amount <= 0)
        {
            throw DomainErrors.Validation("Amount must be greater than zero.", "amount");
        }

        if (amount > MaxAmount)
        {
            throw DomainErrors.Validation("Amount must not exceed 1,000,000.000.", "amount");
        }

        // Value-based: 1.5000 is fine, 1.0005 is not.
        if (decimal.Round(amount, Scale) != amount)
        {
            throw DomainErrors.Validation("Amount must have at most 3 decimal places.", "amount");
        }

        return new Money(amount, currency);
    }
}
