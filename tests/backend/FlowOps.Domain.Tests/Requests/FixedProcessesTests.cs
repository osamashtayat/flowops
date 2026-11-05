using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Tests.Requests;

public sealed class FixedProcessesTests
{
    [Fact]
    public void EquipmentPurchase_IsManagerFinanceProcurement()
    {
        var steps = FixedProcesses.For(RequestType.EquipmentPurchase);

        steps.ShouldBe(
        [
            new ProcessStep(RequestStage.ManagerApproval, WorkTaskKind.Approval, AssigneeRule.RequesterManager),
            new ProcessStep(RequestStage.FinanceApproval, WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer)),
            new ProcessStep(RequestStage.ProcurementFulfillment, WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer)),
        ]);
    }

    [Fact]
    public void Leave_IsManagerThenHr()
    {
        var steps = FixedProcesses.For(RequestType.Leave);

        steps.ShouldBe(
        [
            new ProcessStep(RequestStage.ManagerApproval, WorkTaskKind.Approval, AssigneeRule.RequesterManager),
            new ProcessStep(RequestStage.HrConfirmation, WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.HrOfficer)),
        ]);
    }

    [Theory]
    [InlineData(RequestType.EquipmentPurchase, RequestStage.ProcurementFulfillment)]
    [InlineData(RequestType.Leave, RequestStage.HrConfirmation)]
    public void Next_AfterLastStep_IsNull(RequestType type, RequestStage last) =>
        FixedProcesses.Next(type, last).ShouldBeNull();

    [Fact]
    public void Next_ForStageNotInProcess_Throws() =>
        Should.Throw<ArgumentException>(() => FixedProcesses.Next(RequestType.Leave, RequestStage.FinanceApproval));
}
