using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>One entry of a request's activity log. <see cref="Actor"/> is null for system entries.</summary>
public sealed record ActivityDto(
    Guid Id,
    ActivityType Type,
    UserRef? Actor,
    RequestStage? Stage,
    string? Body,
    DateTimeOffset OccurredAt);
