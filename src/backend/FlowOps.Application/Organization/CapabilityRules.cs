using FlowOps.Application.Common;
using FlowOps.Domain.Organization;

namespace FlowOps.Application.Organization;

/// <summary>
/// The one home of "which capabilities does a user have" (AUTHORIZATION.md §2). Used for
/// <c>GET /api/auth/me</c> and by the Api's coarse policies, so the UI and the server never disagree.
/// </summary>
public static class CapabilityRules
{
    private static readonly string[] OfficerRoles = [Roles.FinanceOfficer, Roles.HrOfficer, Roles.ProcurementOfficer];

    public static IReadOnlyList<Capability> For(IReadOnlyCollection<string> roles, bool isManager)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var capabilities = new List<Capability>(3);
        if (roles.Contains(Roles.Employee))
        {
            capabilities.Add(Capability.RequestsSubmit);
        }

        if (isManager || roles.Any(OfficerRoles.Contains))
        {
            capabilities.Add(Capability.TasksView);
        }

        if (roles.Contains(Roles.Administrator))
        {
            capabilities.Add(Capability.AdminManage);
        }

        return capabilities;
    }
}
