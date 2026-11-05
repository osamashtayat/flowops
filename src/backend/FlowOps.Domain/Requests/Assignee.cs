using FlowOps.Domain.Organization;

namespace FlowOps.Domain.Requests;

/// <summary>Who may act on a task: exactly one of a specific user or a role queue.</summary>
public sealed record Assignee
{
    private Assignee(Guid? userId, string? role)
    {
        UserId = userId;
        Role = role;
    }

    public Guid? UserId { get; }

    public string? Role { get; }

    public static Assignee ForUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user assignee needs a user id.", nameof(userId));
        }

        return new Assignee(userId, null);
    }

    /// <summary>Never Administrator: that role has no process authority, which is why AccessRules grants admins nothing.</summary>
    public static Assignee ForRole(string role)
    {
        if (!Roles.All.Contains(role, StringComparer.Ordinal) || role == Roles.Administrator)
        {
            throw new ArgumentException($"'{role}' cannot be a process role queue.", nameof(role));
        }

        return new Assignee(null, role);
    }
}
