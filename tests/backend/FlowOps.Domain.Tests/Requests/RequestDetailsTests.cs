using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class RequestDetailsTests
{
    private static readonly Money Cost = Money.Create(100m, SupportedCurrencies.Jod);

    // 2026-11-04 21:00:00Z is 2026-11-05 00:00 in Asia/Amman (UTC+3): the company's day has turned, UTC's has not.
    private static readonly DateTimeOffset AmmanMidnight = new(2026, 11, 4, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Nov4 = new(2026, 11, 4);
    private static readonly DateOnly Nov5 = new(2026, 11, 5);

    [Fact]
    public void Leave_StartingToday_IsAccepted()
    {
        var leave = LeaveDetails.Create(Nov5, Nov5, LeaveType.Sick, null, AmmanMidnight, Amman);

        leave.Days.ShouldBe(1);
    }

    [Fact]
    public void Leave_StartYesterdayInCompanyZone_ThrowsValidation_EvenThoughItIsTodayInUtc() =>
        ShouldFailWith(() => LeaveDetails.Create(Nov4, Nov5, LeaveType.Annual, null, AmmanMidnight, Amman), ErrorCodes.Validation);

    [Fact]
    public void Leave_OneSecondBeforeCompanyMidnight_StillAcceptsThatDay() =>
        LeaveDetails.Create(Nov4, Nov4, LeaveType.Annual, null, AmmanMidnight.AddSeconds(-1), Amman).StartDate.ShouldBe(Nov4);

    [Fact]
    public void Leave_EndBeforeStart_ThrowsValidation() =>
        ShouldFailWith(() => LeaveDetails.Create(Nov5.AddDays(3), Nov5.AddDays(2), LeaveType.Annual, null, AmmanMidnight, Amman), ErrorCodes.Validation);

    [Fact]
    public void Leave_ExactlyNinetyDays_IsAccepted() =>
        LeaveDetails.Create(Nov5, Nov5.AddDays(89), LeaveType.Unpaid, null, AmmanMidnight, Amman).Days.ShouldBe(90);

    [Fact]
    public void Leave_NinetyOneDays_ThrowsValidation() =>
        ShouldFailWith(() => LeaveDetails.Create(Nov5, Nov5.AddDays(90), LeaveType.Unpaid, null, AmmanMidnight, Amman), ErrorCodes.Validation);

    [Fact]
    public void Leave_UnknownLeaveType_ThrowsValidation() =>
        ShouldFailWith(() => LeaveDetails.Create(Nov5, Nov5, (LeaveType)99, null, AmmanMidnight, Amman), ErrorCodes.Validation);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Leave_BlankReason_IsStoredAsNull(string? reason) =>
        LeaveDetails.Create(Nov5, Nov5, LeaveType.Other, reason, AmmanMidnight, Amman).Reason.ShouldBeNull();

    [Fact]
    public void Leave_ReasonOverLimit_ThrowsValidation() =>
        ShouldFailWith(
            () => LeaveDetails.Create(Nov5, Nov5, LeaveType.Other, new string('r', RequestLimits.LeaveReasonMaxLength + 1), AmmanMidnight, Amman),
            ErrorCodes.Validation);

    [Fact]
    public void Purchase_TrimsText()
    {
        var details = PurchaseDetails.Create("  Monitor ", " 27-inch ", Cost, " Eye strain ");

        details.ItemName.ShouldBe("Monitor");
        details.Description.ShouldBe("27-inch");
        details.Justification.ShouldBe("Eye strain");
        details.EstimatedCost.ShouldBe(Cost);
    }

    [Theory]
    [InlineData("", "d", "j")]
    [InlineData("i", " ", "j")]
    [InlineData("i", "d", "")]
    public void Purchase_MissingRequiredText_ThrowsValidation(string item, string description, string justification) =>
        ShouldFailWith(() => PurchaseDetails.Create(item, description, Cost, justification), ErrorCodes.Validation);

    [Fact]
    public void Purchase_ItemNameOverLimit_ThrowsValidation() =>
        ShouldFailWith(
            () => PurchaseDetails.Create(new string('i', RequestLimits.ItemNameMaxLength + 1), "d", Cost, "j"),
            ErrorCodes.Validation);
}
