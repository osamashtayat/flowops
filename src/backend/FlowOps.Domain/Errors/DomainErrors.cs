using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Errors;

/// <summary>The one place each domain error's code and message are defined.</summary>
public static class DomainErrors
{
    /// <param name="field">The input field it is about, relative to the value being built (e.g. <c>amount</c>).</param>
    public static DomainException Validation(string message, string? field = null) => new(ErrorCodes.Validation, message, field);

    public static DomainException TaskNotFound() =>
        new(ErrorCodes.NotFound, "The task does not belong to this request.");

    public static DomainException RequestClosed() =>
        new(ErrorCodes.RequestClosed, "The request is closed and can no longer change.");

    public static DomainException TaskAlreadyDecided() =>
        new(ErrorCodes.TaskAlreadyDecided, "This task was already handled.");

    public static DomainException TaskNotCurrent() =>
        new(ErrorCodes.TaskNotCurrent, "This task is not the current step of the request.");

    public static DomainException TaskInvalidAction(WorkTaskKind kind, TaskDecisionAction action) =>
        new(ErrorCodes.TaskInvalidAction, $"'{action}' is not a valid action for a {kind} task.");

    public static DomainException SelfDecision() =>
        new(ErrorCodes.TaskSelfDecision, "You cannot decide a task on your own request.");

    public static DomainException ManagerNotAssigned() =>
        new(ErrorCodes.RequestManagerNotAssigned, "The requester has no manager who can approve this request.");

    public static DomainException RoleChangeSelfApproval() =>
        new(ErrorCodes.RoleChangeSelfApproval, "Another administrator has to decide on your own role change.");

    public static DomainException RoleChangeNotPending() =>
        new(ErrorCodes.RoleChangeNotPending, "This role change is no longer pending.");

    public static DomainException RoleChangeNotProposer() =>
        new(ErrorCodes.RoleChangeNotProposer, "Only the administrator who asked for this role change can cancel it.");
}
