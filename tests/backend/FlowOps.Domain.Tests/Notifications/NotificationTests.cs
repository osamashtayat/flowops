using FlowOps.Domain.Errors;
using FlowOps.Domain.Notifications;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public void MarkRead_KeepsFirstReadTime()
    {
        var notification = Notification.Create(Requester, NotificationType.StepApproved, "Approved by manager", "FO-2026-000001 moved to finance.", null, null, T0);
        notification.IsRead.ShouldBeFalse();

        notification.MarkRead(T0.AddMinutes(1));
        notification.MarkRead(T0.AddMinutes(2));

        notification.ReadAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public void Create_BodyOverLimit_ThrowsValidation() =>
        ShouldFailWith(
            () => Notification.Create(Requester, NotificationType.TaskAssigned, "t", new string('b', Notification.BodyMaxLength + 1), null, null, T0),
            ErrorCodes.Validation);

    [Fact]
    public void Create_WithoutRecipient_Throws() =>
        Should.Throw<ArgumentException>(() => Notification.Create(Guid.Empty, NotificationType.TaskAssigned, "t", "b", null, null, T0));
}
