using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class SubmissionTests
{
    [Fact]
    public void Submit_WithRequesterAsOwnManager_ThrowsManagerNotAssigned() =>
        ShouldFailWith(
            () => Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), PurchaseWorkflow, _ => Assignee.ForUser(Requester), T0),
            ErrorCodes.RequestManagerNotAssigned);

    [Theory]
    [InlineData(RequestType.EquipmentPurchase)]
    [InlineData(RequestType.Leave)]
    public void Submit_ResolvesTheFirstStepThroughTheResolver(RequestType type)
    {
        var asked = new List<WorkflowStep>();
        Assignee Recording(WorkflowStep step)
        {
            asked.Add(step);
            return Resolve(step);
        }

        var request = type == RequestType.Leave
            ? Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), LeaveWorkflow, Recording, T0)
            : Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), PurchaseWorkflow, Recording, T0);

        asked.ShouldBe([WorkflowFor(type).FirstStep]);
        request.OpenTask!.AssigneeUserId.ShouldBe(Manager);
        request.StepKey.ShouldBe(WorkflowFor(type).FirstStep.Key);
        request.WorkflowVersionId.ShouldBe(WorkflowFor(type).Id);
    }

    [Fact]
    public void Submit_WhenResolverBreaksTheFirstStepsRule_ThrowsBeforeAnythingIsCreated() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId, Laptop(), PurchaseWorkflow, _ => Assignee.ForRole(Roles.FinanceOfficer), T0));

    [Fact]
    public void Submit_WithoutResolver_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), LeaveWorkflow, null!, T0));

    [Fact]
    public void Submit_WhenResolverReturnsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), LeaveWorkflow, _ => null!, T0));

    [Fact]
    public void Submit_OnADraftVersion_Throws()
    {
        var draft = Draft([.. LeaveWorkflow.Steps.Select(s => WorkflowStep.Create(s.Key, s.Name, s.Kind, s.Assignee))]);

        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, DepartmentId, AnnualLeave(), draft, Resolve, T0));
    }

    [Fact]
    public void Submit_WithoutRequester_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Guid.Empty, DepartmentId, AnnualLeave(), LeaveWorkflow, Resolve, T0));

    [Fact]
    public void Submit_WithoutDepartment_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Request.SubmitLeave(RequestNumber.Create(2026, 1), Requester, Guid.Empty, AnnualLeave(), LeaveWorkflow, Resolve, T0));

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
