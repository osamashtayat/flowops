using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Organization;

/// <summary>
/// I13: a manager chain has no self-reference and no cycle (DOMAIN_MODEL.md §4). The database also rejects
/// self-management (<c>ck_users_manager_not_self</c>); cycles can only be seen by walking the chain.
/// </summary>
public static class ManagerAssignmentRules
{
    /// <summary>Throws <c>user.manager_cycle</c> (422) if <paramref name="userId"/> reporting to <paramref name="managerId"/> closes a loop.</summary>
    public static async Task EnsureNoCycleAsync(IAppDbContext db, Guid userId, Guid managerId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (managerId == userId)
        {
            throw Cycle();
        }

        // One query for the whole org's manager links (small in Phase 1), then walk upwards in memory.
        var managerOf = await db.Users.AsNoTracking()
            .Where(u => u.ManagerId != null)
            .Select(u => new { u.Id, u.ManagerId })
            .ToDictionaryAsync(u => u.Id, u => u.ManagerId!.Value, ct);
        if (CreatesCycle(userId, managerId, managerOf))
        {
            throw Cycle();
        }
    }

    /// <summary>True when following managers up from <paramref name="managerId"/> reaches <paramref name="userId"/>.</summary>
    public static bool CreatesCycle(Guid userId, Guid managerId, IReadOnlyDictionary<Guid, Guid> managerOf)
    {
        ArgumentNullException.ThrowIfNull(managerOf);
        var visited = new HashSet<Guid>();
        for (Guid? current = managerId; current is { } id; current = managerOf.TryGetValue(id, out var next) ? next : null)
        {
            if (id == userId)
            {
                return true;
            }

            // An existing loop that doesn't involve this user (data from before this rule) must not hang us.
            if (!visited.Add(id))
            {
                return false;
            }
        }

        return false;
    }

    private static BusinessRuleException Cycle() =>
        new(ErrorCodes.UserManagerCycle, "This manager assignment would make the user (indirectly) manage themselves.");
}
