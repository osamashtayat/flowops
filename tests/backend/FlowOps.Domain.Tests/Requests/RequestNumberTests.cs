using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Tests.Requests;

public sealed class RequestNumberTests
{
    [Fact]
    public void Create_FormatsYearAndZeroPaddedSequence() =>
        RequestNumber.Create(2026, 42).Value.ShouldBe("FO-2026-000042");

    [Fact]
    public void Parse_RoundTripsCreatedNumber() =>
        RequestNumber.Parse(RequestNumber.Create(2026, 1234567).Value).ShouldBe(RequestNumber.Create(2026, 1234567));

    [Theory]
    [InlineData("")]
    [InlineData("FO-26-000001")]
    [InlineData("fo-2026-000001")]
    [InlineData("FO-2026-00001")]
    [InlineData("FO-2026-000001 ")]
    [InlineData("XX-2026-000001")]
    public void Parse_RejectsMalformedNumber(string value) =>
        Should.Throw<FormatException>(() => RequestNumber.Parse(value));
}
