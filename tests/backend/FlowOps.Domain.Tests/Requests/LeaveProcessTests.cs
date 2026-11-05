using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.Requests.PurchaseProcessTests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class LeaveProcessTests
{
    private static readonly DateTimeOffset T1 = T0.AddHours(1);
    private static readonly DateTimeOffset T2 = T0.AddHours(2);

    [Fact]
    public void FullPath_ManagerThenHr_EndsCompleted()
    {
        var request = SubmitLeave();
        request.Type.ShouldBe(RequestType.Leave);
        request.Leave.ShouldNotBeNull();
        request.Purchase.ShouldBeNull();
        request.Stage.ShouldBe(RequestStage.ManagerApproval);
        request.OpenTask!.AssigneeUserId.ShouldBe(Manager);
        request.Activities.Select(a => a.Type).ShouldBe([ActivityType.Submitted, ActivityType.TaskAssigned]);

        var managerTask = request.OpenTask!;
        var count = request.Activities.Count;
        var r1 = request.ApproveCurrent(Manager, T1);

        request.Status.ShouldBe(RequestStatus.InReview);
        request.Stage.ShouldBe(RequestStage.HrConfirmation);
        var hrTask = r1.NextTask.ShouldNotBeNull();
        hrTask.Kind.ShouldBe(WorkTaskKind.Approval);
        hrTask.AssigneeRole.ShouldBe(Roles.HrOfficer);
        var step1 = request.AppendedSince(count);
        step1.Length.ShouldBe(2);
        ActivityShouldBe(step1[0], ActivityType.Approved, Manager, managerTask.Id, RequestStage.ManagerApproval, T1);
        ActivityShouldBe(step1[1], ActivityType.TaskAssigned, null, hrTask.Id, RequestStage.HrConfirmation, T1);

        count = request.Activities.Count;
        var r2 = request.ApproveCurrent(HrOfficer, T2);

        request.Status.ShouldBe(RequestStatus.Completed);
        request.Stage.ShouldBe(RequestStage.Closed);
        request.ClosedAt.ShouldBe(T2);
        request.OpenTask.ShouldBeNull();
        r2.NextTask.ShouldBeNull();
        TaskShouldBeDone(hrTask, WorkTaskOutcome.Approved, HrOfficer, T2, null);
        var step2 = request.AppendedSince(count);
        step2.Length.ShouldBe(2);
        ActivityShouldBe(step2[0], ActivityType.Approved, HrOfficer, hrTask.Id, RequestStage.HrConfirmation, T2);
        ActivityShouldBe(step2[1], ActivityType.Completed, null, null, RequestStage.Closed, T2);
        request.Tasks.Count.ShouldBe(2);
    }
}
