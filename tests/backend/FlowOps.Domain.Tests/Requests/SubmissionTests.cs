using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class SubmissionTests
{
    [Fact]
    public void Submit_WithRequesterAsOwnManager_ThrowsManagerNotAssigned() =>
        ShouldFailWith(
            () => Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), _ => Assignee.ForUser(Requester), T0),
            ErrorCodes.RequestManagerNotAssigned);

    [Theory]
    [InlineData(RequestType.EquipmentPurchase)]
    [InlineData(RequestType.Leave)]
    public void Submit_ResolvesTheFirstStepThroughTheResolver(RequestType type)
    {
        var asked = new List<ProcessStep>();
        Assignee Recording(ProcessStep step)
        {
            asked.Add(step);
            return Resolve(step);
        }

        var request = type == RequestType.Leave
            ? Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), Recording, T0)
            : Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), Recording, T0);

        asked.ShouldBe([FixedProcesses.First(type)]);
        request.OpenTask!.AssigneeUserId.ShouldBe(Manager);
        request.Stage.ShouldBe(FixedProcesses.First(type).Stage);
    }

    [Fact]
    public void Submit_WhenResolverBreaksTheFirstStepsRule_ThrowsBeforeAnythingIsCreated() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), _ => Assignee.ForRole(Roles.FinanceOfficer), T0));

    [Fact]
    public void Submit_WithoutResolver_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), null!, T0));

    [Fact]
    public void Submit_WhenResolverReturnsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), _ => null!, T0));

    [Fact]
    public void Submit_WithoutRequester_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Guid.Empty, DepartmentId, AnnualLeave(), Resolve, T0));

    [Fact]
    public void Submit_WithoutDepartment_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, Guid.Empty, AnnualLeave(), Resolve, T0));

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
