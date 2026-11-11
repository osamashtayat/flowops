using FlowOps.Domain.Organization;

namespace FlowOps.Domain.Requests;

/// <summary>
/// How a process step's assignee is chosen: the requester's manager, or a role queue.
/// The Application layer (<c>TaskAssigneeResolver</c>) turns a rule into an <see cref="Assignee"/>.
/// </summary>
public sealed record AssigneeRule
{
    private AssigneeRule(AssigneeRuleKind kind, string? role)
    {
        Kind = kind;
        Role = role;
    }

    public AssigneeRuleKind Kind { get; }

    /// <summary>The role queue, or <c>null</c> for <see cref="RequesterManager"/>.</summary>
    public string? Role { get; }

    public bool IsRequesterManager => Kind == AssigneeRuleKind.RequesterManager;

    public static AssigneeRule RequesterManager { get; } = new(AssigneeRuleKind.RequesterManager, null);

    /// <summary>A role queue. Never Administrator: that role has no process authority (AUTHORIZATION.md §2).</summary>
    public static AssigneeRule ForRole(string role) =>
        Roles.All.Contains(role, StringComparer.Ordinal) && role != Roles.Administrator
            ? new AssigneeRule(AssigneeRuleKind.Role, role)
            : throw new ArgumentException($"'{role}' cannot be a process role queue.", nameof(role));

    /// <summary>Rebuilds a stored rule.</summary>
    public static AssigneeRule From(AssigneeRuleKind kind, string? role) => kind switch
    {
        AssigneeRuleKind.RequesterManager when role is null => RequesterManager,
        AssigneeRuleKind.Role when role is not null => ForRole(role),
        _ => throw new ArgumentException($"A {kind} rule cannot have the role '{role}'.", nameof(role)),
    };

    /// <summary>Whether a resolved assignee is of the shape this rule demands.</summary>
    public bool Accepts(Assignee assignee)
    {
        ArgumentNullException.ThrowIfNull(assignee);
        return IsRequesterManager ? assignee.UserId is not null : assignee.Role == Role;
    }
}
