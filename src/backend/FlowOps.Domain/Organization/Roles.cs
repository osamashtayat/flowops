namespace FlowOps.Domain.Organization;

/// <summary>Identity role names. "Manager" is deliberately not a role: it is derived from <c>ManagerId</c> links.</summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Employee = "Employee";
    public const string FinanceOfficer = "FinanceOfficer";
    public const string HrOfficer = "HrOfficer";
    public const string ProcurementOfficer = "ProcurementOfficer";

    public static IReadOnlyList<string> All { get; } =
        [Administrator, Employee, FinanceOfficer, HrOfficer, ProcurementOfficer];
}
