using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class PurchaseProcessTests
{
    private static readonly DateTimeOffset T1 = T0.AddHours(1);
    private static readonly DateTimeOffset T2 = T0.AddHours(2);
    private static readonly DateTimeOffset T3 = T0.AddHours(3);

    [Fact]
    public void Submit_StartsAtManagerApproval_WithManagerTaskAndTwoActivities()
    {
        var request = SubmitPurchase();

        request.Status.ShouldBe(RequestStatus.InReview);
        request.Stage.ShouldBe(RequestStage.ManagerApproval);
        request.Type.ShouldBe(RequestType.EquipmentPurchase);
        request.Number.Value.ShouldBe("FO-2026-000001");
        request.RequesterId.ShouldBe(Requester);
        request.DepartmentId.ShouldBe(DepartmentId);
        request.SubmittedAt.ShouldBe(T0);
        request.ClosedAt.ShouldBeNull();
        request.Purchase.ShouldNotBeNull();
        request.Leave.ShouldBeNull();

        var task = request.Tasks.ShouldHaveSingleItem();
        task.ShouldBeSameAs(request.OpenTask);
        task.Kind.ShouldBe(WorkTaskKind.Approval);
        task.Stage.ShouldBe(RequestStage.ManagerApproval);
        task.AssigneeUserId.ShouldBe(Manager);
        task.AssigneeRole.ShouldBeNull();
        task.Status.ShouldBe(WorkTaskStatus.Open);
        task.Outcome.ShouldBeNull();
        task.DecidedById.ShouldBeNull();
        task.DecidedAt.ShouldBeNull();
        task.CreatedAt.ShouldBe(T0);

        request.Activities.Count.ShouldBe(2);
        ActivityShouldBe(request.Activities[0], ActivityType.Submitted, Requester, null, RequestStage.ManagerApproval, T0);
        ActivityShouldBe(request.Activities[1], ActivityType.TaskAssigned, null, task.Id, RequestStage.ManagerApproval, T0);
    }

    [Fact]
    public void FullPath_ManagerFinanceProcurement_EndsCompleted()
    {
        var request = SubmitPurchase();

        // Manager approves → finance step.
        var managerTask = request.OpenTask!;
        var count = request.Activities.Count;
        var r1 = request.DecideTask(managerTask.Id, Manager, TaskDecision.Approve("Looks fine"), Resolve, T1);

        request.Status.ShouldBe(RequestStatus.InReview);
        request.Stage.ShouldBe(RequestStage.FinanceApproval);
        TaskShouldBeDone(managerTask, WorkTaskOutcome.Approved, Manager, T1, "Looks fine");
        var financeTask = r1.NextTask.ShouldNotBeNull();
        financeTask.ShouldBeSameAs(request.OpenTask);
        financeTask.Kind.ShouldBe(WorkTaskKind.Approval);
        financeTask.AssigneeRole.ShouldBe(Roles.FinanceOfficer);
        financeTask.AssigneeUserId.ShouldBeNull();
        r1.DecidedTask.ShouldBeSameAs(managerTask);
        r1.Status.ShouldBe(RequestStatus.InReview);
        var step1 = request.AppendedSince(count);
        step1.Length.ShouldBe(2);
        ActivityShouldBe(step1[0], ActivityType.Approved, Manager, managerTask.Id, RequestStage.ManagerApproval, T1, "Looks fine");
        ActivityShouldBe(step1[1], ActivityType.TaskAssigned, null, financeTask.Id, RequestStage.FinanceApproval, T1);

        // Finance approves → Approved, awaiting procurement.
        count = request.Activities.Count;
        var r2 = request.ApproveCurrent(FinanceOfficer, T2);

        request.Status.ShouldBe(RequestStatus.Approved);
        request.Stage.ShouldBe(RequestStage.ProcurementFulfillment);
        TaskShouldBeDone(financeTask, WorkTaskOutcome.Approved, FinanceOfficer, T2, null);
        var procurementTask = r2.NextTask.ShouldNotBeNull();
        procurementTask.Kind.ShouldBe(WorkTaskKind.Fulfillment);
        procurementTask.AssigneeRole.ShouldBe(Roles.ProcurementOfficer);
        r2.Status.ShouldBe(RequestStatus.Approved);
        var step2 = request.AppendedSince(count);
        step2.Length.ShouldBe(2);
        ActivityShouldBe(step2[0], ActivityType.Approved, FinanceOfficer, financeTask.Id, RequestStage.FinanceApproval, T2);
        ActivityShouldBe(step2[1], ActivityType.TaskAssigned, null, procurementTask.Id, RequestStage.ProcurementFulfillment, T2);

        // Procurement fulfils → Completed.
        count = request.Activities.Count;
        var r3 = request.DecideTask(procurementTask.Id, ProcurementOfficer, TaskDecision.Fulfill("Ordered PO-77"), Resolve, T3);

        request.Status.ShouldBe(RequestStatus.Completed);
        request.Stage.ShouldBe(RequestStage.Closed);
        request.ClosedAt.ShouldBe(T3);
        request.IsClosed.ShouldBeTrue();
        request.OpenTask.ShouldBeNull();
        TaskShouldBeDone(procurementTask, WorkTaskOutcome.Fulfilled, ProcurementOfficer, T3, "Ordered PO-77");
        r3.NextTask.ShouldBeNull();
        r3.Status.ShouldBe(RequestStatus.Completed);
        r3.Stage.ShouldBe(RequestStage.Closed);
        var step3 = request.AppendedSince(count);
        step3.Length.ShouldBe(2);
        ActivityShouldBe(step3[0], ActivityType.Fulfilled, ProcurementOfficer, procurementTask.Id, RequestStage.ProcurementFulfillment, T3, "Ordered PO-77");
        ActivityShouldBe(step3[1], ActivityType.Completed, null, null, RequestStage.Closed, T3);

        request.Tasks.Count.ShouldBe(3);
        request.Activities.Count.ShouldBe(8);
    }

    internal static void TaskShouldBeDone(WorkTask task, WorkTaskOutcome outcome, Guid actor, DateTimeOffset at, string? comment)
    {
        task.Status.ShouldBe(WorkTaskStatus.Done);
        task.Outcome.ShouldBe(outcome);
        task.DecidedById.ShouldBe(actor);
        task.DecidedAt.ShouldBe(at);
        task.Comment.ShouldBe(comment);
    }

    internal static void ActivityShouldBe(
        RequestActivity activity, ActivityType type, Guid? actor, Guid? taskId, RequestStage? stage, DateTimeOffset at, string? body = null)
    {
        activity.Type.ShouldBe(type);
        activity.ActorId.ShouldBe(actor);
        activity.TaskId.ShouldBe(taskId);
        activity.Stage.ShouldBe(stage);
        activity.OccurredAt.ShouldBe(at);
        activity.Body.ShouldBe(body);
    }
}
