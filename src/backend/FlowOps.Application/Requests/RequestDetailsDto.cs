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
    bool CanComment,
    IReadOnlyList<RouteStepDto> Route)
{
    /// <summary>The step the request waits at; <c>null</c> once it is closed. Replaces <c>stage</c> (ADR-0014 Q2).</summary>
    public StepRef? Step { get; init; } = StepRef.For(Stage);
}

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
    DateTimeOffset CreatedAt)
{
    /// <summary>The step this task decides. Replaces <c>stage</c> (ADR-0014 Q2).</summary>
    public StepRef Step { get; init; } = StepRef.ForTask(Stage);
}

/// <summary>Exactly one of <see cref="User"/> and <see cref="Role"/> is set.</summary>
public sealed record TaskAssigneeDto(UserRef? User, Role? Role);

/// <summary>
/// One step of the request's route (ADR-0014 Q5): the steps it went through or skipped, the current one, then the steps
/// still planned for it. A closed request has no <see cref="RouteStepState.Planned"/> or <see cref="RouteStepState.Current"/> steps.
/// </summary>
public sealed record RouteStepDto(StepRef Step, WorkTaskKind Kind, RouteStepState State);

public enum RouteStepState
{
    /// <summary>A task of this step was decided (approved, rejected or fulfilled; the outcome is on the task).</summary>
    Done,

    /// <summary>Its condition was false, so no task was created (a <c>StepSkipped</c> activity says why). From P2-BE-05.</summary>
    Skipped,

    /// <summary>The open task's step.</summary>
    Current,

    /// <summary>Will run after the current step, given the request's facts and its pinned workflow version.</summary>
    Planned,
}
