using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Tasks;

/// <summary>One inbox row: the task and a summary of its request.</summary>
public sealed record InboxItemDto(
    Guid TaskId,
    WorkTaskKind Kind,
    RequestStage Stage,
    DateTimeOffset CreatedAt,
    string Version,
    WorkTaskStatus Status,
    WorkTaskOutcome? Outcome,
    DateTimeOffset? DecidedAt,
    InboxRequestDto Request);

/// <summary>The request summary plus <see cref="KeyDetail"/>, e.g. "JOD 1,250.500" or "3 days · Annual".</summary>
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
    string KeyDetail);
