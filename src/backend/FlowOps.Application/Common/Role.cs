namespace FlowOps.Application.Common;

/// <summary>
/// Role names on the wire. Mirrors <c>FlowOps.Domain.Organization.Roles</c> (the authoritative list);
/// a test asserts the two stay identical.
/// </summary>
public enum Role
{
    Administrator,
    Employee,
    FinanceOfficer,
    HrOfficer,
    ProcurementOfficer,
}
