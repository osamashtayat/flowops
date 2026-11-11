using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>
/// The one resolver submission and decisions share (#83). No database: the requester is already loaded.
/// A process whose first step is a role queue needs only its process entry; the resolver needs no manager for it.
/// </summary>
public sealed class TaskAssigneeResolverTests
{
    private static readonly Guid RequesterId = Guid.CreateVersion7();
    private static readonly Guid ManagerId = Guid.CreateVersion7();

    private static TaskAssigneeResolver.Requester Requester(Guid? managerId, bool managerIsActive = true) =>
        new(RequesterId, "Lina", Guid.CreateVersion7(), managerId, managerIsActive, IsActive: true, [Roles.Employee]);

    [Fact]
    public void RequesterManagerStep_GoesToTheActiveManager()
    {
        var step = new ProcessStep(RequestStage.ManagerApproval, WorkTaskKind.Approval, AssigneeRule.RequesterManager);

        TaskAssigneeResolver.ResolverFor(Requester(ManagerId))(step).ShouldBe(Assignee.ForUser(ManagerId));
    }

    [Fact]
    public void RoleQueueStep_GoesToTheRole_WithoutNeedingAManager()
    {
        var step = new ProcessStep(RequestStage.HrConfirmation, WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.HrOfficer));

        TaskAssigneeResolver.ResolverFor(Requester(managerId: null))(step).ShouldBe(Assignee.ForRole(Roles.HrOfficer));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RequesterManagerStep_WithoutAnActiveManager_Is422ManagerNotAssigned(bool hasManager, bool managerIsActive)
    {
        var step = new ProcessStep(RequestStage.ManagerApproval, WorkTaskKind.Approval, AssigneeRule.RequesterManager);
        var resolve = TaskAssigneeResolver.ResolverFor(Requester(hasManager ? ManagerId : null, managerIsActive));

        Should.Throw<BusinessRuleException>(() => resolve(step)).Code.ShouldBe(ErrorCodes.RequestManagerNotAssigned);
    }
}
