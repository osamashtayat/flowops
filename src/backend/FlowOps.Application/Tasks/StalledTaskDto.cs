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
    DateTimeOffset OpenSince);

public enum StalledTaskReason
{
    NoEligibleRoleMember,
    AssigneeInactive,
}
