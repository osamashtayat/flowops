using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Workflows;

// The Phase 2a workflow contract (ADR-0014, API_CONTRACT.md §10). Contract skeleton from P2-DOC-01; the endpoints that
// use these types are implemented in P2-BE-03 (persistence), P2-BE-05 (conditions, route) and P2-BE-07 (administration).

/// <summary>One workflow per request type, with its Published and Draft versions.</summary>
public sealed record WorkflowSummaryDto(
    Guid Id,
    RequestType RequestType,
    string Name,
    WorkflowVersionSummaryDto? Published,
    WorkflowVersionSummaryDto? Draft);

/// <summary>A version without its steps: the history list and the summary's Published/Draft entries.</summary>
public sealed record WorkflowVersionSummaryDto(
    Guid Id,
    int Number,
    WorkflowVersionStatus Status,
    DateTimeOffset CreatedAt,
    UserRef CreatedBy,
    DateTimeOffset? PublishedAt,
    UserRef? PublishedBy,
    DateTimeOffset? RetiredAt);

/// <summary>A version with its ordered steps. <see cref="Version"/> is the opaque concurrency token for draft edits.</summary>
public sealed record WorkflowVersionDto(
    Guid Id,
    Guid WorkflowId,
    RequestType RequestType,
    int Number,
    WorkflowVersionStatus Status,
    IReadOnlyList<WorkflowStepDto> Steps,
    DateTimeOffset CreatedAt,
    UserRef CreatedBy,
    DateTimeOffset? PublishedAt,
    UserRef? PublishedBy,
    DateTimeOffset? RetiredAt,
    string Version);

/// <summary>A step as stored and shown. Steps run in list order; the first never has a condition.</summary>
public sealed record WorkflowStepDto(
    string Key,
    string Name,
    WorkTaskKind Kind,
    AssigneeRuleDto Assignee,
    ConditionDto? Condition,
    bool CanReadRestrictedDetails);

/// <summary>
/// Who a step is assigned to (ADR-0014 §4). <see cref="Role"/> is set for <c>Role</c> and <c>DepartmentRole</c>;
/// <see cref="DepartmentId"/> for <c>DepartmentRole</c>; <see cref="UserId"/> for <c>SpecificUser</c>; otherwise null.
/// <c>Administrator</c> is never a valid role here.
/// </summary>
public sealed record AssigneeRuleDto(AssigneeRuleKind Kind, Role? Role, Guid? DepartmentId, Guid? UserId);

/// <summary>
/// One typed comparison: the step runs only when <see cref="Fact"/> <see cref="Operator"/> <see cref="Values"/> holds.
/// <see cref="Values"/> holds exactly one value, except for <c>IsOneOf</c> (one or more). Values are strings on the wire:
/// a money amount as <c>"1000.000"</c> (JOD), a number as <c>"5"</c>, an enum by name, a date as <c>yyyy-MM-dd</c>.
/// </summary>
public sealed record ConditionDto(FactKey Fact, ConditionOperator Operator, IReadOnlyList<string> Values);

/// <summary><c>GET /api/admin/workflows/catalogue</c>: what the editor may offer. Built from code, never from data.</summary>
public sealed record WorkflowCatalogueDto(IReadOnlyList<RequestTypeFactsDto> RequestTypes, IReadOnlyList<AssigneeRuleKind> AssigneeRules);

public sealed record RequestTypeFactsDto(RequestType RequestType, IReadOnlyList<FactDto> Facts);

/// <summary><see cref="Options"/> lists the allowed values of an <c>Enum</c> fact (e.g. leave types), otherwise empty.</summary>
public sealed record FactDto(FactKey Key, string Label, FactValueType ValueType, IReadOnlyList<ConditionOperator> Operators, IReadOnlyList<string> Options);

/// <summary>Replaces a draft's steps. <see cref="Version"/> is the draft's last-seen token (409 when stale).</summary>
public sealed record UpdateWorkflowDraftRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string? Version { get; init; }

    [Required]
    [MinLength(1)]
    [MaxLength(20)]
    public IReadOnlyList<WorkflowStepInput>? Steps { get; init; }
}

/// <summary>A step as the editor sends it. Validation beyond field shape is reported by validate/publish, not 400.</summary>
public sealed record WorkflowStepInput
{
    /// <summary>Lower-case letters, digits and hyphens, unique within the version, e.g. <c>finance-approval</c>.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public string? Key { get; init; }

    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string? Name { get; init; }

    [Required]
    public WorkTaskKind? Kind { get; init; }

    [Required]
    public AssigneeRuleDto? Assignee { get; init; }

    public ConditionDto? Condition { get; init; }

    public bool CanReadRestrictedDetails { get; init; }
}

/// <summary>Publishing needs the draft's last-seen token, like every other optimistic write.</summary>
public sealed record PublishWorkflowRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string? Version { get; init; }
}

/// <summary>
/// Result of validating a draft. Publishing is refused while <see cref="Errors"/> is non-empty;
/// <see cref="Warnings"/> never block (ADR-0014 Q6).
/// </summary>
public sealed record WorkflowValidationDto(IReadOnlyList<WorkflowIssueDto> Errors, IReadOnlyList<WorkflowIssueDto> Warnings);

/// <summary><see cref="StepKey"/> and <see cref="Field"/> point the editor at the problem; either may be null.</summary>
public sealed record WorkflowIssueDto(WorkflowIssueCode Code, string Message, string? StepKey, string? Field);

public enum WorkflowIssueCode
{
    [JsonStringEnumMemberName("workflow.no_steps")]
    NoSteps,

    [JsonStringEnumMemberName("workflow.first_step_conditional")]
    FirstStepConditional,

    [JsonStringEnumMemberName("workflow.duplicate_key")]
    DuplicateKey,

    [JsonStringEnumMemberName("workflow.unknown_fact")]
    UnknownFact,

    [JsonStringEnumMemberName("workflow.operator_not_allowed")]
    OperatorNotAllowed,

    [JsonStringEnumMemberName("workflow.invalid_value")]
    InvalidValue,

    [JsonStringEnumMemberName("workflow.administrator_assignee")]
    AdministratorAssignee,

    [JsonStringEnumMemberName("workflow.invalid_assignee")]
    InvalidAssignee,

    /// <summary>A Fulfillment step must be the last step, and there is at most one.</summary>
    [JsonStringEnumMemberName("workflow.fulfillment_not_last")]
    FulfillmentNotLast,

    /// <summary>Warning only: no active user currently satisfies the step's assignee rule.</summary>
    [JsonStringEnumMemberName("workflow.assignee_unavailable")]
    AssigneeUnavailable,
}

/// <summary>Sample facts for a route preview. Only the facts of the request type in question are read.</summary>
public sealed record RouteFactsInput
{
    /// <summary>Purchase amount in JOD, e.g. <c>"1500.000"</c>.</summary>
    public MoneyDto? Amount { get; init; }

    public LeaveType? LeaveType { get; init; }

    [Range(1, 90)]
    public int? Days { get; init; }
}

/// <summary><c>POST …/versions/{id}/preview</c> (admin): the route a request with these facts would take.</summary>
public sealed record WorkflowPreviewRequest
{
    [Required]
    public RouteFactsInput? Facts { get; init; }
}

/// <summary><c>POST /api/requests/route-preview</c> (requester, before submitting).</summary>
public sealed record RoutePreviewRequest
{
    [Required]
    public RequestType? Type { get; init; }

    [Required]
    public RouteFactsInput? Facts { get; init; }
}

/// <summary>Every step of the version in order: <c>Planned</c> or <c>Skipped</c> for these facts (never Done/Current).</summary>
public sealed record RoutePreviewDto(IReadOnlyList<RouteStepDto> Steps);
