using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Requests;

public sealed class CommentTests
{
    [Fact]
    public void AddComment_AppendsCommentActivityWithoutChangingState()
    {
        var request = SubmitPurchase();
        var at = T0.AddMinutes(30);

        var activity = request.AddComment(Manager, " Which model? ", at);

        activity.ShouldBeSameAs(request.Activities[^1]);
        activity.Type.ShouldBe(ActivityType.CommentAdded);
        activity.ActorId.ShouldBe(Manager);
        activity.TaskId.ShouldBeNull();
        activity.StepKey.ShouldBeNull();
        activity.Body.ShouldBe("Which model?");
        activity.OccurredAt.ShouldBe(at);
        request.StepKey.ShouldBe(Steps.ManagerApproval);
        request.Status.ShouldBe(RequestStatus.InReview);
    }

    [Fact]
    public void AddComment_OnClosedRequest_IsAllowed()
    {
        var request = SubmitPurchase();
        request.RejectCurrent(Manager, "No budget");

        request.AddComment(Requester, "Understood, will resubmit in Q1", T0);

        request.Activities[^1].Type.ShouldBe(ActivityType.CommentAdded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AddComment_Blank_ThrowsValidation(string body) =>
        ShouldFailWith(() => SubmitPurchase().AddComment(Manager, body, T0), ErrorCodes.Validation);

    [Fact]
    public void AddComment_OverLimit_ThrowsValidation() =>
        ShouldFailWith(
            () => SubmitPurchase().AddComment(Manager, new string('x', RequestLimits.CommentMaxLength + 1), T0),
            ErrorCodes.Validation);
}
