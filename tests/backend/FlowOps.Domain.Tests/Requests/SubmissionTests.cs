using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class SubmissionTests
{
    [Fact]
    public void Submit_WithRequesterAsOwnManager_ThrowsManagerNotAssigned() =>
        ShouldFailWith(
            () => Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), Requester, T0),
            ErrorCodes.RequestManagerNotAssigned);

    [Fact]
    public void Submit_WithoutRequester_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Guid.Empty, DepartmentId, AnnualLeave(), Manager, T0));

    [Fact]
    public void Submit_WithoutDepartment_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, Guid.Empty, AnnualLeave(), Manager, T0));

    [Theory]
    [InlineData(2026, 42, "FO-2026-000042")]
    [InlineData(2027, 1, "FO-2027-000001")]
    [InlineData(2026, 1234567, "FO-2026-1234567")]
    public void RequestNumber_FormatsYearAndSequence(int year, long sequence, string expected) =>
        RequestNumber.Create(year, sequence).Value.ShouldBe(expected);

    [Fact]
    public void RequestNumber_RejectsNonPositiveSequence() =>
        Should.Throw<ArgumentOutOfRangeException>(() => RequestNumber.Create(2026, 0));
}
