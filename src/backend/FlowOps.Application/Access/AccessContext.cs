using System.Collections.Frozen;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Access;

/// <summary>
/// Who is asking, as resource checks see them (AUTHORIZATION.md §4). Only <see cref="AccessRules"/> creates one,
/// from the database and the current user, so callers can't build it from stale claims or a client-supplied id.
/// Immutable: roles are copied once and every check reads that copy.
/// </summary>
public sealed class AccessContext
{
    private readonly Lazy<Func<WorkTask, bool>> _isAssigned;

    internal AccessContext(Guid userId, IEnumerable<string> roles, bool isActive)
    {
        UserId = userId;
        RoleArray = [.. roles.Distinct(StringComparer.Ordinal)];
        Roles = RoleArray.ToFrozenSet(StringComparer.Ordinal);
        IsActive = isActive;
        // One compile per context (i.e. per request), not per check.
        _isAssigned = new Lazy<Func<WorkTask, bool>>(() => TaskEligibility.AssignedTo(this).Compile());
    }

    public Guid UserId { get; }

    public IReadOnlySet<string> Roles { get; }

    public bool IsActive { get; }

    /// <summary>For EF translation: <c>Contains</c> on an array parameter becomes <c>= ANY(@roles)</c>.</summary>
    internal string[] RoleArray { get; }

    /// <summary><see cref="TaskEligibility.AssignedTo"/> compiled, so memory and SQL use the very same expression.</summary>
    internal bool IsAssigned(WorkTask task) => _isAssigned.Value(task);
}
