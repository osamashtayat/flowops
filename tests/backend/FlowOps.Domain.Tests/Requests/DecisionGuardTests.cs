using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class DecisionGuardTests
{
    [Fact]
    public void Approve_TaskAlreadyDone_ThrowsAlreadyDecided()
    {
        var request = SubmitPurchase();
        var managerTask = request.OpenTask!;
        request.ApproveCurrent(Manager);

        AssertRejectedWithoutChange(
            request,
            () => request.DecideTask(managerTask.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), Resolve, T0),
            ErrorCodes.TaskAlreadyDecided);
    }

    [Fact]
    public void Decide_OnRejectedRequest_ThrowsRequestClosed()
    {
        var request = SubmitPurchase();
        var task = request.OpenTask!;
        request.RejectCurrent(Manager, "No budget");

        AssertRejectedWithoutChange(
            request,
            () => request.DecideTask(task.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), Resolve, T0),
            ErrorCodes.RequestClosed);
    }

    [Fact]
    public void Decide_OnCompletedRequest_ThrowsRequestClosed()
    {
        var request = SubmitLeave();
        request.ApproveCurrent(Manager);
        var hrTask = request.OpenTask!;
        request.ApproveCurrent(HrOfficer);

        AssertRejectedWithoutChange(
            request,
            () => request.DecideTask(hrTask.Id, HrOfficer, TaskDecision.Reject("Changed my mind"), WorkflowFor(request.Type), Resolve, T0),
            ErrorCodes.RequestClosed);
    }

    [Fact]
    public void Decide_NonCurrentTask_ThrowsTaskNotCurrent()
    {
        // Unreachable through the aggregate's API (I4 keeps one open task at the current stage), so this
        // simulates an inconsistent row loaded from storage, exactly as persistence hydrates private setters.
        var request = SubmitPurchase();
        var managerTask = request.OpenTask!;
        typeof(Request).GetProperty(nameof(Request.StepKey))!.SetValue(request, Steps.FinanceApproval);

        AssertRejectedWithoutChange(
            request,
            () => request.DecideTask(managerTask.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), Resolve, T0),
            ErrorCodes.TaskNotCurrent);
    }

    [Fact]
    public void AllowedActions_FollowTheTaskKind()
    {
        var request = SubmitPurchase();
        request.OpenTask!.AllowedActions.ShouldBe([TaskDecisionAction.Approve, TaskDecisionAction.Reject]);

        request.ApproveCurrent(Manager);
        request.ApproveCurrent(FinanceOfficer);

        request.OpenTask!.Kind.ShouldBe(WorkTaskKind.Fulfillment);
        request.OpenTask!.AllowedActions.ShouldBe([TaskDecisionAction.Fulfill]);
    }

    [Fact]
    public void Reject_FulfillmentTask_ThrowsInvalidAction()
    {
        var request = SubmitPurchase();
        request.ApproveCurrent(Manager);
        request.ApproveCurrent(FinanceOfficer);

        AssertRejectedWithoutChange(
            request,
            () => request.RejectCurrent(ProcurementOfficer, "Vendor out of stock"),
            ErrorCodes.TaskInvalidAction);
    }

    [Fact]
    public void Fulfill_ApprovalTask_ThrowsInvalidAction()
    {
        var request = SubmitPurchase();

        AssertRejectedWithoutChange(request, () => request.FulfillCurrent(Manager), ErrorCodes.TaskInvalidAction);
    }

    [Fact]
    public void Approve_ByRequester_ThrowsSelfDecision()
    {
        // A finance officer submits a purchase; their own request reaches the finance queue.
        var request = SubmitPurchase(requester: FinanceOfficer);
        request.ApproveCurrent(Manager);

        AssertRejectedWithoutChange(request, () => request.ApproveCurrent(FinanceOfficer), ErrorCodes.TaskSelfDecision);
    }

    [Fact]
    public void Reject_ByRequester_ThrowsSelfDecision()
    {
        var request = SubmitPurchase(requester: FinanceOfficer);
        request.ApproveCurrent(Manager);

        AssertRejectedWithoutChange(request, () => request.RejectCurrent(FinanceOfficer, "Withdrawing"), ErrorCodes.TaskSelfDecision);
    }

    [Fact]
    public void Decide_TaskOfAnotherRequest_ThrowsNotFound()
    {
        var request = SubmitPurchase();
        var otherTask = SubmitLeave().OpenTask!;

        AssertRejectedWithoutChange(
            request,
            () => request.DecideTask(otherTask.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), Resolve, T0),
            ErrorCodes.NotFound);
    }

    [Fact]
    public void Decide_WhenNextAssigneeCannotBeResolved_LeavesRequestUnchanged()
    {
        var request = SubmitPurchase();
        var before = Snapshot(request);

        Should.Throw<InvalidOperationException>(() => request.DecideTask(
            request.OpenTask!.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), _ => throw new InvalidOperationException("resolver failed"), T0));

        Snapshot(request).ShouldBe(before);
    }

    [Fact]
    public void Decide_WithAssigneeNotMatchingStepRule_LeavesRequestUnchanged()
    {
        // FinanceApproval needs the FinanceOfficer role queue, not a specific user.
        var request = SubmitPurchase();
        var before = Snapshot(request);

        Should.Throw<ArgumentException>(() => request.DecideTask(
            request.OpenTask!.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), _ => Assignee.ForUser(FinanceOfficer), T0));

        Snapshot(request).ShouldBe(before);
    }

    [Fact]
    public void Decide_WhenResolverReturnsNull_LeavesRequestUnchanged()
    {
        var request = SubmitPurchase();
        var before = Snapshot(request);

        Should.Throw<ArgumentNullException>(() => request.DecideTask(
            request.OpenTask!.Id, Manager, TaskDecision.Approve(null), WorkflowFor(request.Type), _ => null!, T0));

        Snapshot(request).ShouldBe(before);
    }

    [Fact]
    public void Decide_WithEmptyActorId_LeavesRequestUnchanged()
    {
        var request = SubmitPurchase();
        var before = Snapshot(request);

        Should.Throw<ArgumentException>(() => request.DecideTask(
            request.OpenTask!.Id, Guid.Empty, TaskDecision.Approve(null), WorkflowFor(request.Type), Resolve, T0));

        Snapshot(request).ShouldBe(before);
    }

    [Fact]
    public void Decide_WithAVersionTheRequestIsNotPinnedTo_LeavesRequestUnchanged()
    {
        var request = SubmitPurchase();
        var before = Snapshot(request);

        Should.Throw<ArgumentException>(() => request.DecideTask(
            request.OpenTask!.Id, Manager, TaskDecision.Approve(null), LeaveWorkflow, Resolve, T0));

        Snapshot(request).ShouldBe(before);
    }

    private static void AssertRejectedWithoutChange(Request request, Action decide, string code)
    {
        var before = Snapshot(request);

        ShouldFailWith(decide, code);

        Snapshot(request).ShouldBe(before);
    }

    private static string Snapshot(Request request) =>
        $"{request.Status}|{request.StepKey}|{request.ClosedAt}|activities={request.Activities.Count}|" +
        string.Join(",", request.Tasks.Select(t => $"{t.Id}:{t.Status}:{t.Outcome}:{t.DecidedById}"));
}
