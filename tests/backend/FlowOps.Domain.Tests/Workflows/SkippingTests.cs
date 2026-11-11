using FlowOps.Domain.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Workflows;

/// <summary>
/// DOMAIN_MODEL.md §7.5, TESTING.md §3.7: the next step is the first later step whose condition holds; each skipped step
/// appends exactly one <c>StepSkipped</c> (no actor, the step, the condition in words) and no task.
/// </summary>
public sealed class SkippingTests
{
    private const string FinanceRule = "Only if amount is greater than 1,000.000 JOD";

    /// <summary>The owner's rule: Manager → Finance if amount &gt; 1000 JOD, else → Procurement.</summary>
    private static readonly WorkflowVersion OwnersRule = Published(
        WorkflowStep.Create(Steps.ManagerApproval, "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
        WorkflowStep.Create(Steps.FinanceApproval, "Finance approval", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer),
            Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1000.000"])),
        WorkflowStep.Create(Steps.ProcurementFulfillment, "Procurement fulfilment", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer)));

    private static Request Purchase(WorkflowVersion workflow, decimal amount) =>
        Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, DepartmentId,
            PurchaseDetails.Create("Laptop", "14 inch", Money.Create(amount, SupportedCurrencies.Jod), "Broken"), workflow, Resolve, T0);

    private static DecisionResult Approve(Request request, WorkflowVersion workflow, Guid actor) =>
        request.DecideTask(request.OpenTask!.Id, actor, TaskDecision.Approve(null), workflow, Resolve, T0.AddHours(1));

    [Theory]
    [InlineData(400.000)]
    [InlineData(1000.000)]
    public void AtOrBelow1000_ManagerApprovalSkipsFinanceAndOpensProcurement(double amount)
    {
        var request = Purchase(OwnersRule, (decimal)amount);
        var before = request.Activities.Count;

        var result = Approve(request, OwnersRule, Manager);

        result.NextTask!.StepKey.ShouldBe(Steps.ProcurementFulfillment);
        request.Status.ShouldBe(RequestStatus.Approved);
        request.Tasks.ShouldNotContain(t => t.StepKey == Steps.FinanceApproval);
        var appended = request.AppendedSince(before);
        appended.Select(a => a.Type).ShouldBe([ActivityType.Approved, ActivityType.StepSkipped, ActivityType.TaskAssigned]);
        var skip = appended[1];
        (skip.ActorId, skip.TaskId, skip.Step, skip.Body).ShouldBe(
            (null, null, new StepRef(Steps.FinanceApproval, "Finance approval"), FinanceRule));
    }

    [Fact]
    public void Above1000_ManagerApprovalOpensFinanceAndSkipsNothing()
    {
        var request = Purchase(OwnersRule, 1000.001m);

        var result = Approve(request, OwnersRule, Manager);

        result.NextTask!.StepKey.ShouldBe(Steps.FinanceApproval);
        request.Status.ShouldBe(RequestStatus.InReview);
        request.Activities.ShouldNotContain(a => a.Type == ActivityType.StepSkipped);
    }

    [Fact]
    public void AllLaterStepsSkipped_CompletesTheRequest()
    {
        var onlyLargeOnes = Published(
            WorkflowStep.Create(Steps.ManagerApproval, "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
            WorkflowStep.Create(Steps.FinanceApproval, "Finance approval", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer),
                Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1000.000"])),
            WorkflowStep.Create(Steps.ProcurementFulfillment, "Procurement fulfilment", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer),
                Condition.Create(FactKey.Amount, ConditionOperator.GreaterThanOrEqual, ["5000.000"])));
        var request = Purchase(onlyLargeOnes, 400m);
        var before = request.Activities.Count;

        var result = Approve(request, onlyLargeOnes, Manager);

        result.NextTask.ShouldBeNull();
        (request.Status, request.Step, request.ClosedAt).ShouldBe((RequestStatus.Completed, null, T0.AddHours(1)));
        request.AppendedSince(before).Select(a => (a.Type, a.StepKey)).ShouldBe([
            (ActivityType.Approved, Steps.ManagerApproval),
            (ActivityType.StepSkipped, Steps.FinanceApproval),
            (ActivityType.StepSkipped, Steps.ProcurementFulfillment),
            (ActivityType.Completed, null)]);
        request.Tasks.ShouldHaveSingleItem();
    }

    [Fact]
    public void Rejection_EvaluatesNoLaterStep()
    {
        var request = Purchase(OwnersRule, 400m);
        var before = request.Activities.Count;

        request.DecideTask(request.OpenTask!.Id, Manager, TaskDecision.Reject("No budget"), OwnersRule, Resolve, T0.AddHours(1));

        request.Status.ShouldBe(RequestStatus.Rejected);
        request.AppendedSince(before).Select(a => a.Type).ShouldBe([ActivityType.Rejected]);
    }

    [Fact]
    public void LeaveConditions_ReadLeaveTypeAndDays()
    {
        var hrForLongOrSick = Published(
            WorkflowStep.Create(Steps.ManagerApproval, "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
            WorkflowStep.Create(Steps.HrConfirmation, "HR confirmation", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.HrOfficer),
                Condition.Create(FactKey.Days, ConditionOperator.GreaterThan, ["2"])));
        var threeDays = Request.SubmitLeave(RequestNumber.Create(2026, 2), Requester, DepartmentId, AnnualLeave(), hrForLongOrSick, Resolve, T0);

        Approve(threeDays, hrForLongOrSick, Manager).NextTask!.StepKey.ShouldBe(Steps.HrConfirmation);
    }
}
