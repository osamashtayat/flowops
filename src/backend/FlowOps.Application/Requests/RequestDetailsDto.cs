using FlowOps.Application.Access;
using System.Text.Json.Serialization;
using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// <c>GET /api/requests/{id}</c>: the summary fields plus details, tasks and what the caller may do.
/// <see cref="AvailableActions"/> comes from the same <c>TaskEligibility</c> used for decisions.
/// </summary>
public sealed record RequestDetailsDto(
    Guid Id,
    string Number,
    RequestType Type,
    string Title,
    UserRef Requester,
    DepartmentRef Department,
    RequestStatus Status,
    RequestStage Stage,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ClosedAt,
    PurchaseDetailsDto? Purchase,
    LeaveDetailsDto? Leave,
    IReadOnlyList<WorkTaskDto> Tasks,
    AvailableActionsDto? AvailableActions,
    bool CanComment);

public sealed record PurchaseDetailsDto(string ItemName, string Description, MoneyDto EstimatedCost, string Justification);

/// <summary><see cref="Reason"/> is null and <see cref="ReasonHidden"/> true for viewers outside AUTHORIZATION.md §5.</summary>
public sealed record LeaveDetailsDto(DateOnly StartDate, DateOnly EndDate, int Days, LeaveType LeaveType, string? Reason, bool ReasonHidden);

public sealed record WorkTaskDto(
    Guid Id,
    WorkTaskKind Kind,
    RequestStage Stage,
    WorkTaskStatus Status,
    TaskAssigneeDto Assignee,
    WorkTaskOutcome? Outcome,
    UserRef? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Comment,
    DateTimeOffset CreatedAt);

/// <summary>Exactly one of <see cref="User"/> and <see cref="Role"/> is set.</summary>
public sealed record TaskAssigneeDto(UserRef? User, Role? Role);
