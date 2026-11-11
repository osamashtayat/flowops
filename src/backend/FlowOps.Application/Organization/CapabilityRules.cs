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

    /// <summary>Administration doesn't depend on the manager relationship, so no isManager is needed to ask.</summary>
    public static bool CanAdminister(IReadOnlyCollection<string> roles) =>
        For(roles, isManager: false).Contains(Capability.AdminManage);

    public static IReadOnlyList<Capability> For(IReadOnlyCollection<string> roles, bool isManager)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var capabilities = new List<Capability>(4);
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
            // ADR-0014 Q1: one administrator edits and publishes; no process authority comes with it (AUTHORIZATION.md §2).
            capabilities.Add(Capability.WorkflowsManage);
        }

        return capabilities;
    }
}
