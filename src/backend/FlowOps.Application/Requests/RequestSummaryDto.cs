using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>A request in lists. <see cref="Title"/>: item name for purchases, e.g. "Annual leave · 12–14 Nov" for leave.</summary>
public sealed record RequestSummaryDto(
    Guid Id,
    string Number,
    RequestType Type,
    string Title,
    UserRef Requester,
    DepartmentRef Department,
    RequestStatus Status,
    RequestStage Stage,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ClosedAt)
{
    /// <summary>The step the request waits at; <c>null</c> once it is closed. Replaces <c>stage</c> (ADR-0014 Q2).</summary>
    public StepRef? Step { get; init; } = StepRef.For(Stage);
}
