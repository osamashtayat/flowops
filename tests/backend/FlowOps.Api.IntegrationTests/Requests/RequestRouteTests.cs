using FlowOps.Application.Requests;
using FlowOps.Domain.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>
/// The step and route fields added by P2-DOC-01 (ADR-0014 Q2/Q5), over real aggregates and no database. Skipped steps
/// come from conditions (P2-BE-05).
/// </summary>
public sealed class RequestRouteTests
{
    private static readonly Guid Requester = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    // The seeded v1 (DOMAIN_MODEL.md §7.4), built in memory; WorkflowSeedTests checks the database holds the same steps.
    private static readonly WorkflowVersion PurchaseV1 = Published(
        WorkflowStep.Create("manager-approval", "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
        WorkflowStep.Create("finance-approval", "Finance approval", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer)),
        WorkflowStep.Create("procurement-fulfillment", "Procurement fulfilment", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer)));

    /// <summary>The owner's rule (P2-BE-05 AC): Finance only if amount &gt; 1000 JOD.</summary>
    private static readonly WorkflowVersion OwnersRule = Published(
        WorkflowStep.Create("manager-approval", "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
        WorkflowStep.Create("finance-approval", "Finance approval", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer),
            Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1000.000"])),
        WorkflowStep.Create("procurement-fulfillment", "Procurement fulfilment", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer)));

    private static readonly WorkflowVersion LeaveV1 = Published(
        WorkflowStep.Create("manager-approval", "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
        WorkflowStep.Create("hr-confirmation", "HR confirmation", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.HrOfficer)));

    [Fact]
    public void Purchase_AtManager_ShowsCurrentThenPlanned()
    {
        var request = Purchase();

        Route(request).ShouldBe([
            ("manager-approval", RouteStepState.Current),
            ("finance-approval", RouteStepState.Planned),
            ("procurement-fulfillment", RouteStepState.Planned)]);
    }

    [Fact]
    public void Purchase_AtProcurement_ShowsDoneStepsThenCurrent()
    {
        var request = Purchase();
        Approve(request, Manager);
        Approve(request, Guid.CreateVersion7());

        Route(request).ShouldBe([
            ("manager-approval", RouteStepState.Done),
            ("finance-approval", RouteStepState.Done),
            ("procurement-fulfillment", RouteStepState.Current)]);
    }

    [Fact]
    public void Rejected_ShowsOnlyTheStepsItWentThrough()
    {
        var request = Purchase();
        request.DecideTask(request.OpenTask!.Id, Manager, TaskDecision.Reject("No budget"), PurchaseV1, Resolve, Now);

        Route(request).ShouldBe([("manager-approval", RouteStepState.Done)]);
    }

    [Fact]
    public void CompletedLeave_ShowsEveryStepDone()
    {
        var request = Request.SubmitLeave(RequestNumber.Create(2026, 2), Requester, Guid.CreateVersion7(),
            LeaveDetails.Create(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), LeaveType.Annual, null, Now, TimeZoneInfo.Utc),
            LeaveV1, Resolve, Now);
        Approve(request, Manager);
        Approve(request, Guid.CreateVersion7());

        Route(request).ShouldBe([("manager-approval", RouteStepState.Done), ("hr-confirmation", RouteStepState.Done)]);
    }

    [Fact]
    public void OwnersRule_At400_LeavesFinanceOutOfThePlan()
    {
        var request = Purchase(OwnersRule, 400m);

        Route(request, OwnersRule).ShouldBe([
            ("manager-approval", RouteStepState.Current),
            ("procurement-fulfillment", RouteStepState.Planned)]);
    }

    [Fact]
    public void OwnersRule_At1500_PlansFinance()
    {
        var request = Purchase(OwnersRule, 1500m);

        Route(request, OwnersRule).ShouldBe([
            ("manager-approval", RouteStepState.Current),
            ("finance-approval", RouteStepState.Planned),
            ("procurement-fulfillment", RouteStepState.Planned)]);
    }

    [Fact]
    public void OwnersRule_At400_ShowsFinanceSkippedOncePassed_AndOnlyTheStepsItWentThroughWhenClosed()
    {
        var request = Purchase(OwnersRule, 400m);
        Decide(request, OwnersRule, Manager);

        Route(request, OwnersRule).ShouldBe([
            ("manager-approval", RouteStepState.Done),
            ("finance-approval", RouteStepState.Skipped),
            ("procurement-fulfillment", RouteStepState.Current)]);

        Decide(request, OwnersRule, Guid.CreateVersion7());

        request.Status.ShouldBe(RequestStatus.Completed);
        Route(request, OwnersRule).ShouldBe([
            ("manager-approval", RouteStepState.Done),
            ("finance-approval", RouteStepState.Skipped),
            ("procurement-fulfillment", RouteStepState.Done)]);
    }

    [Theory]
    [InlineData(400, RouteStepState.Skipped)]
    [InlineData(1000, RouteStepState.Skipped)]
    [InlineData(1500, RouteStepState.Planned)]
    public void Preview_ListsEveryStepPlannedOrSkipped(int amount, RouteStepState finance) =>
        RequestRoute.Preview(OwnersRule, new RequestFacts(Money.Create(amount, SupportedCurrencies.Jod), null, null))
            .Select(r => (r.Step.Key, r.State)).ShouldBe([
                ("manager-approval", RouteStepState.Planned),
                ("finance-approval", finance),
                ("procurement-fulfillment", RouteStepState.Planned)]);

    private static Request Purchase() =>
        Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, Guid.CreateVersion7(),
            PurchaseDetails.Create("Laptop", "14 inch", Money.Create(1250.500m, SupportedCurrencies.Jod), "Broken"), PurchaseV1, Resolve, Now);

    private static Assignee Resolve(WorkflowStep step) =>
        step.Assignee.IsRequesterManager ? Assignee.ForUser(Manager) : Assignee.ForRole(step.Assignee.Role!);

    private static void Approve(Request request, Guid actor)
    {
        var task = request.OpenTask!;
        var decision = task.Kind == WorkTaskKind.Fulfillment ? TaskDecision.Fulfill(null) : TaskDecision.Approve(null);
        request.DecideTask(task.Id, actor, decision, V1(request), Resolve, Now);
    }

    private static (string Key, RouteStepState State)[] Route(Request request, WorkflowVersion? pinned = null) =>
        [.. RequestRoute.For(pinned ?? V1(request), request.Tasks,
                request.Activities.Where(a => a.Type == ActivityType.StepSkipped).Select(a => a.Step!), request.Facts)
            .Select(r => (r.Step.Key, r.State))];

    private static WorkflowVersion V1(Request request) => request.Type == RequestType.Leave ? LeaveV1 : PurchaseV1;

    private static Request Purchase(WorkflowVersion workflow, decimal amount) =>
        Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, Guid.CreateVersion7(),
            PurchaseDetails.Create("Laptop", "14 inch", Money.Create(amount, SupportedCurrencies.Jod), "Broken"), workflow, Resolve, Now);

    private static void Decide(Request request, WorkflowVersion workflow, Guid actor)
    {
        var task = request.OpenTask!;
        var decision = task.Kind == WorkTaskKind.Fulfillment ? TaskDecision.Fulfill(null) : TaskDecision.Approve(null);
        request.DecideTask(task.Id, actor, decision, workflow, Resolve, Now);
    }

    private static WorkflowVersion Published(params WorkflowStep[] steps)
    {
        var version = WorkflowVersion.CreateDraft(Guid.CreateVersion7(), 1, steps, Guid.CreateVersion7(), Now);
        version.Publish(Guid.CreateVersion7(), Now);
        return version;
    }
}
