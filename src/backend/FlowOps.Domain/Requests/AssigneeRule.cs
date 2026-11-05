using FlowOps.Domain.Organization;

namespace FlowOps.Domain.Requests;

/// <summary>
/// How a process step's assignee is chosen: the requester's manager, or a role queue.
/// The Application layer (<c>TaskAssigneeResolver</c>) turns a rule into an <see cref="Assignee"/>.
/// </summary>
public sealed record AssigneeRule
{
    private AssigneeRule(string? role) => Role = role;

    /// <summary>The role queue, or <c>null</c> for <see cref="RequesterManager"/>.</summary>
    public string? Role { get; }

    public bool IsRequesterManager => Role is null;

    public static AssigneeRule RequesterManager { get; } = new((string?)null);

    public static AssigneeRule ForRole(string role) =>
        Roles.All.Contains(role, StringComparer.Ordinal)
            ? new AssigneeRule(role)
            : throw new ArgumentException($"Unknown role '{role}'.", nameof(role));

    /// <summary>Whether a resolved assignee is of the shape this rule demands.</summary>
    public bool Accepts(Assignee assignee)
    {
        ArgumentNullException.ThrowIfNull(assignee);
        return IsRequesterManager ? assignee.UserId is not null : assignee.Role == Role;
    }
}
