using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.Requests.PurchaseProcessTests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class RejectionTests
{
    private static readonly DateTimeOffset RejectedAt = T0.AddHours(5);

    public static TheoryData<RequestType, string> ApprovalSteps => new()
    {
        { RequestType.EquipmentPurchase, Steps.ManagerApproval },
        { RequestType.EquipmentPurchase, Steps.FinanceApproval },
        { RequestType.Leave, Steps.ManagerApproval },
        { RequestType.Leave, Steps.HrConfirmation },
    };

    [Theory]
    [MemberData(nameof(ApprovalSteps))]
    public void Reject_AtAnyApprovalStep_ClosesAsRejectedWithoutNextTask(RequestType type, string stage)
    {
        var request = AdvanceTo(type, stage);
        var task = request.OpenTask!;
        var tasksBefore = request.Tasks.Count;
        var count = request.Activities.Count;

        var result = request.RejectCurrent(ApproverFor(stage), "  Over budget  ", RejectedAt);

        request.Status.ShouldBe(RequestStatus.Rejected);
        request.StepKey.ShouldBe(Steps.Closed);
        request.ClosedAt.ShouldBe(RejectedAt);
        request.OpenTask.ShouldBeNull();
        request.Tasks.Count.ShouldBe(tasksBefore);
        result.NextTask.ShouldBeNull();
        result.Status.ShouldBe(RequestStatus.Rejected);
        TaskShouldBeDone(task, WorkTaskOutcome.Rejected, ApproverFor(stage), RejectedAt, "Over budget");
        var appended = request.AppendedSince(count).ShouldHaveSingleItem();
        ActivityShouldBe(appended, ActivityType.Rejected, ApproverFor(stage), task.Id, stage, RejectedAt, "Over budget");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutReason_ThrowsValidation(string? reason) =>
        ShouldFailWith(() => TaskDecision.Reject(reason!), Errors.ErrorCodes.Validation);

    [Fact]
    public void Reject_WithReasonOverLimit_ThrowsValidation() =>
        ShouldFailWith(() => TaskDecision.Reject(new string('x', RequestLimits.CommentMaxLength + 1)), Errors.ErrorCodes.Validation);

    [Fact]
    public void Reject_WithReasonAtLimit_IsAccepted() =>
        TaskDecision.Reject(new string('x', RequestLimits.CommentMaxLength)).Comment!.Length.ShouldBe(RequestLimits.CommentMaxLength);

    private static Request AdvanceTo(RequestType type, string stage)
    {
        var request = type == RequestType.Leave ? SubmitLeave() : SubmitPurchase();
        if (stage != Steps.ManagerApproval)
        {
            request.ApproveCurrent(Manager);
        }

        request.StepKey.ShouldBe(stage);
        return request;
    }

    private static Guid ApproverFor(string stage) => stage switch
    {
        Steps.ManagerApproval => Manager,
        Steps.FinanceApproval => FinanceOfficer,
        Steps.HrConfirmation => HrOfficer,
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };
}
