using FlowOps.Application.Requests;
using FlowOps.Domain.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>
/// The step and route fields added by P2-DOC-01 (ADR-0014 Q2/Q5), over real aggregates and no database. Skipped steps
/// arrive with conditions in P2-BE-05.
/// </summary>
public sealed class RequestRouteTests
{
    private static readonly Guid Requester = Guid.CreateVersion7();
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StepRef_CoversEveryStage_AndClosedIsNotAStep()
    {
        var steps = Enum.GetValues<RequestStage>().Where(s => s != RequestStage.Closed).Select(StepRef.ForTask).ToList();

        steps.Select(s => s.Key).ShouldBe(["manager-approval", "finance-approval", "hr-confirmation", "procurement-fulfillment"], ignoreOrder: true);
        steps.ShouldAllBe(s => !string.IsNullOrWhiteSpace(s.Name));
        StepRef.For(RequestStage.Closed).ShouldBeNull();
    }

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
        request.DecideTask(request.OpenTask!.Id, Manager, TaskDecision.Reject("No budget"), Resolve, Now);

        Route(request).ShouldBe([("manager-approval", RouteStepState.Done)]);
    }

    [Fact]
    public void CompletedLeave_ShowsEveryStepDone()
    {
        var request = Request.SubmitLeave(RequestNumber.Create(2026, 2), Requester, Guid.CreateVersion7(),
            LeaveDetails.Create(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), LeaveType.Annual, null, Now, TimeZoneInfo.Utc),
            Resolve, Now);
        Approve(request, Manager);
        Approve(request, Guid.CreateVersion7());

        Route(request).ShouldBe([("manager-approval", RouteStepState.Done), ("hr-confirmation", RouteStepState.Done)]);
    }

    private static Request Purchase() =>
        Request.SubmitPurchase(RequestNumber.Create(2026, 1), Requester, Guid.CreateVersion7(),
            PurchaseDetails.Create("Laptop", "14 inch", Money.Create(1250.500m, SupportedCurrencies.Jod), "Broken"), Resolve, Now);

    private static Assignee Resolve(ProcessStep step) =>
        step.Assignee.IsRequesterManager ? Assignee.ForUser(Manager) : Assignee.ForRole(step.Assignee.Role!);

    private static void Approve(Request request, Guid actor)
    {
        var task = request.OpenTask!;
        var decision = task.Kind == WorkTaskKind.Fulfillment ? TaskDecision.Fulfill(null) : TaskDecision.Approve(null);
        request.DecideTask(task.Id, actor, decision, Resolve, Now);
    }

    private static (string Key, RouteStepState State)[] Route(Request request) =>
        [.. RequestRoute.For(request.Type, request.Tasks, request.Status).Select(r => (r.Step.Key, r.State))];
}
