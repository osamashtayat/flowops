using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Common;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("0.001")]
    [InlineData("1250.5")]
    [InlineData("1250.500")]
    [InlineData("1.5000")]
    [InlineData("1000000.000")]
    public void Create_ValidAmount_Succeeds(string amount)
    {
        var money = Money.Create(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), SupportedCurrencies.Jod);

        money.Currency.ShouldBe("JOD");
        money.Amount.ShouldBe(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    [InlineData("-5")]
    [InlineData("1000000.001")]
    [InlineData("2000000")]
    [InlineData("1.0005")]
    [InlineData("0.0001")]
    public void Create_InvalidAmount_ThrowsValidation(string amount) =>
        ShouldFailWith(
            () => Money.Create(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), SupportedCurrencies.Jod),
            ErrorCodes.Validation);

    [Theory]
    [InlineData("USD")]
    [InlineData("jod")]
    [InlineData("")]
    public void Create_UnsupportedCurrency_ThrowsValidation(string currency) =>
        ShouldFailWith(() => Money.Create(10m, currency), ErrorCodes.Validation);

    [Fact]
    public void Money_WithSameValues_AreEqual() =>
        Money.Create(10.5m, SupportedCurrencies.Jod).ShouldBe(Money.Create(10.5m, SupportedCurrencies.Jod));
}
