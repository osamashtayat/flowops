using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Tasks;

/// <summary>An open task nobody can currently decide. Metadata only: admins see no request details.</summary>
public sealed record StalledTaskDto(
    Guid TaskId,
    Guid RequestId,
    string RequestNumber,
    RequestType RequestType,
    StepRef Step,
    TaskAssigneeDto Assignee,
    StalledTaskReason Reason,
    DateTimeOffset OpenSince);

public enum StalledTaskReason
{
    NoEligibleRoleMember,
    AssigneeInactive,
}
