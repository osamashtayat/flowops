using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Tasks;

/// <summary>
/// One inbox row: the task and a summary of its request. <see cref="Actions"/> is what the caller may do with an
/// open task (same source as <see cref="AvailableActionsDto.Actions"/>) and empty for a decided one.
/// </summary>
public sealed record InboxItemDto(
    Guid TaskId,
    WorkTaskKind Kind,
    RequestStage Stage,
    DateTimeOffset CreatedAt,
    string Version,
    WorkTaskStatus Status,
    WorkTaskOutcome? Outcome,
    DateTimeOffset? DecidedAt,
    IReadOnlyList<TaskAction> Actions,
    InboxRequestDto Request)
{
    /// <summary>The step this task decides. Replaces <c>stage</c> (ADR-0014 Q2).</summary>
    public StepRef Step { get; init; } = StepRef.ForTask(Stage);
}

/// <summary>The request summary plus <see cref="KeyDetail"/>, e.g. "1,250.500 JOD" or "3 days · Annual".</summary>
public sealed record InboxRequestDto(
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
    string KeyDetail)
{
    /// <summary>The step the request waits at; <c>null</c> once it is closed. Replaces <c>stage</c> (ADR-0014 Q2).</summary>
    public StepRef? Step { get; init; } = StepRef.For(Stage);
}
