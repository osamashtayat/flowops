using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Workflows;

/// <summary>DOMAIN_MODEL.md §7.1: Draft → Published → Retired is the only lifecycle; only a Draft changes (TESTING.md §3.7).</summary>
public sealed class WorkflowVersionTests
{
    private static readonly DateTimeOffset T1 = T0.AddHours(1);

    private static WorkflowStep Manager() =>
        WorkflowStep.Create("manager-approval", "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager);

    private static WorkflowStep Procurement() =>
        WorkflowStep.Create("procurement-fulfillment", "Procurement fulfilment", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer));

    [Fact]
    public void CreateDraft_NumbersStepsInListOrder()
    {
        var draft = Draft(Manager(), Procurement());

        draft.Status.ShouldBe(WorkflowVersionStatus.Draft);
        draft.CreatedById.ShouldBe(WorkflowAdmin);
        draft.Steps.Select(s => (s.Order, s.Key)).ShouldBe([(0, "manager-approval"), (1, "procurement-fulfillment")]);
        draft.PublishedAt.ShouldBeNull();
    }

    [Fact]
    public void Publish_Draft_RecordsWhoAndWhen()
    {
        var draft = Draft(Manager(), Procurement());

        draft.Publish(WorkflowAdmin, T1);

        draft.Status.ShouldBe(WorkflowVersionStatus.Published);
        draft.PublishedAt.ShouldBe(T1);
        draft.PublishedById.ShouldBe(WorkflowAdmin);
        draft.RetiredAt.ShouldBeNull();
    }

    [Fact]
    public void Retire_Published_RecordsWhen()
    {
        var version = Published(Manager());

        version.Retire(T1);

        version.Status.ShouldBe(WorkflowVersionStatus.Retired);
        version.RetiredAt.ShouldBe(T1);
    }

    [Fact]
    public void Publish_PublishedOrRetired_ThrowsVersionNotDraft()
    {
        var published = Published(Manager());
        var retired = Published(Manager());
        retired.Retire(T1);

        ShouldFailWith(() => published.Publish(WorkflowAdmin, T1), ErrorCodes.WorkflowVersionNotDraft);
        ShouldFailWith(() => retired.Publish(WorkflowAdmin, T1), ErrorCodes.WorkflowVersionNotDraft);
        published.PublishedAt.ShouldBe(T0);
        retired.Status.ShouldBe(WorkflowVersionStatus.Retired);
    }

    [Fact]
    public void Retire_DraftOrRetired_IsAProgrammingError()
    {
        var retired = Published(Manager());
        retired.Retire(T1);

        Should.Throw<InvalidOperationException>(() => Draft(Manager()).Retire(T1));
        Should.Throw<InvalidOperationException>(() => retired.Retire(T1.AddHours(1)));
        retired.RetiredAt.ShouldBe(T1);
    }

    [Fact]
    public void ReplaceSteps_PublishedOrRetired_ThrowsAndKeepsTheSteps()
    {
        var published = Published(Manager(), Procurement());
        var retired = Published(Manager(), Procurement());
        retired.Retire(T1);

        foreach (var version in new[] { published, retired })
        {
            ShouldFailWith(() => version.ReplaceSteps([Manager()]), ErrorCodes.WorkflowVersionNotDraft);
            version.Steps.Select(s => s.Key).ShouldBe(["manager-approval", "procurement-fulfillment"]);
        }
    }

    [Fact]
    public void ReplaceSteps_Draft_ReplacesThem()
    {
        var draft = Draft(Manager(), Procurement());

        draft.ReplaceSteps([Manager()]);

        draft.Steps.Select(s => s.Key).ShouldBe(["manager-approval"]);
    }

    [Fact]
    public void Steps_WithTheSameKeyTwice_AreRejected() =>
        ShouldFailWith(() => Draft(Manager(), Manager()), ErrorCodes.Validation);

    [Fact]
    public void Publish_WithoutSteps_ThrowsInvalid() =>
        ShouldFailWith(() => Draft().Publish(WorkflowAdmin, T1), ErrorCodes.WorkflowInvalid);

    [Fact]
    public void Publish_WithFulfillmentBeforeTheLastStep_ThrowsInvalid() =>
        ShouldFailWith(() => Draft(Procurement(), Manager()).Publish(WorkflowAdmin, T1), ErrorCodes.WorkflowInvalid);

    [Fact]
    public void Publish_WithTwoFulfillmentSteps_ThrowsInvalid()
    {
        var second = WorkflowStep.Create("handover", "Handover", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer));

        ShouldFailWith(() => Draft(Manager(), Procurement(), second).Publish(WorkflowAdmin, T1), ErrorCodes.WorkflowInvalid);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("manager approval")]
    [InlineData("-manager")]
    [InlineData("manager-")]
    [InlineData("manager--approval")]
    [InlineData("")]
    public void StepKey_MustBeALowerCaseSlug(string key) =>
        ShouldFailWith(() => WorkflowStep.Create(key, "Name", WorkTaskKind.Approval, AssigneeRule.RequesterManager), ErrorCodes.Validation);

    [Fact]
    public void StepKey_LongerThanTheLimit_IsRejected() =>
        ShouldFailWith(
            () => WorkflowStep.Create(new string('a', WorkflowStep.KeyMaxLength + 1), "Name", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
            ErrorCodes.Validation);

    [Fact]
    public void StepAfter_WalksVersionOrder()
    {
        PurchaseWorkflow.StepAfter("manager-approval")!.Key.ShouldBe("finance-approval");
        PurchaseWorkflow.StepAfter("procurement-fulfillment").ShouldBeNull();
        LeaveWorkflow.StepAfter("hr-confirmation").ShouldBeNull();
    }

    [Fact]
    public void StepAfter_AKeyNotInTheVersion_Throws() =>
        Should.Throw<ArgumentException>(() => LeaveWorkflow.StepAfter("finance-approval"));

    [Fact]
    public void Administrator_IsNeverAnAssignee()
    {
        Should.Throw<ArgumentException>(() => AssigneeRule.ForRole(Roles.Administrator));
        Should.Throw<ArgumentException>(() => AssigneeRule.From(AssigneeRuleKind.Role, Roles.Administrator));
        Should.Throw<ArgumentException>(() => Assignee.ForRole(Roles.Administrator));
    }

    [Fact]
    public void RequestsTasksAndActivities_StoreTheStepKeyAndANameSnapshot()
    {
        var request = SubmitLeave();

        request.Step.ShouldBe(new StepRef("manager-approval", "Manager approval"));
        request.OpenTask!.Step.ShouldBe(request.Step);
        request.Activities.Select(a => a.Step).ShouldAllBe(s => s == request.Step);

        request.ApproveCurrent(TestOrg.Manager);
        request.ApproveCurrent(HrOfficer);

        request.Step.ShouldBeNull();
        request.Tasks.Select(t => t.Step.Name).ShouldBe(["Manager approval", "HR confirmation"]);
        request.Activities[^1].Type.ShouldBe(ActivityType.Completed);
        request.Activities[^1].Step.ShouldBeNull();
    }
}
