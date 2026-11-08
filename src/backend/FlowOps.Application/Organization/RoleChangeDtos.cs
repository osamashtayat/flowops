using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FlowOps.Application.Common;
using FlowOps.Domain.Organization;

namespace FlowOps.Application.Organization;

/// <summary>An administrator's own role change (ADR-0012 §6). <see cref="Status"/> already accounts for expiry.</summary>
public sealed record RoleChangeDto(
    Guid Id,
    UserRef User,
    IReadOnlyList<Role> ProposedRoles,
    IReadOnlyList<Role> PreviousRoles,
    RoleChangeStatus Status,
    DateTimeOffset ProposedAt,
    DateTimeOffset ExpiresAt,
    UserRef? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? RejectionReason,
    string Version,
    IReadOnlyList<RoleChangeAction> AvailableActions);

public enum RoleChangeAction
{
    [JsonStringEnumMemberName("approve")]
    Approve,

    [JsonStringEnumMemberName("reject")]
    Reject,

    [JsonStringEnumMemberName("cancel")]
    Cancel,
}

/// <summary>
/// <c>PUT /api/users/{id}/roles</c>: either the user with their new roles (applied at once), or the pending change when
/// an administrator edits their own roles (ADR-0012 §3). Exactly one is set.
/// </summary>
public sealed record RoleUpdateOutcome(UserDto? User, RoleChangeDto? PendingChange);

/// <summary><c>GET /api/admin/role-changes</c>, newest first.</summary>
public sealed record RoleChangeListQuery : PageRequest
{
    public RoleChangeStatus? Status { get; init; }
}

/// <summary><c>POST /api/admin/role-changes/{id}/approve</c> and <c>/cancel</c>.</summary>
public sealed record RoleChangeDecisionRequest
{
    [Required]
    [StringLength(VersionToken.MaxLength)]
    public string Version { get; init; } = "";
}

/// <summary><c>POST /api/admin/role-changes/{id}/reject</c>.</summary>
public sealed record RejectRoleChangeRequest
{
    [Required]
    [StringLength(VersionToken.MaxLength)]
    public string Version { get; init; } = "";

    [Required]
    [StringLength(RoleChange.RejectionReasonMaxLength, MinimumLength = 1)]
    public string Reason { get; init; } = "";
}
