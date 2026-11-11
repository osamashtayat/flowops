using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Tasks;

/// <summary>An open task nobody can currently decide. Metadata only: admins see no request details.</summary>
public sealed record StalledTaskDto(
    Guid TaskId,
    Guid RequestId,
    string RequestNumber,
    RequestType RequestType,
    RequestStage Stage,
    TaskAssigneeDto Assignee,
    StalledTaskReason Reason,
    DateTimeOffset OpenSince)
{
    /// <summary>The step of the stalled task. Replaces <c>stage</c> (ADR-0014 Q2).</summary>
    public StepRef Step { get; init; } = StepRef.ForTask(Stage);
}

public enum StalledTaskReason
{
    NoEligibleRoleMember,
    AssigneeInactive,
}
