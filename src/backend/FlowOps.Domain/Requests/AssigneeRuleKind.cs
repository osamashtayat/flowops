namespace FlowOps.Domain.Requests;

/// <summary>
/// The kinds of <see cref="AssigneeRule"/> (DOMAIN_MODEL.md §7.3). <see cref="DepartmentRole"/>, <see cref="SpecificUser"/>
/// and <see cref="ManagerOfManager"/> are in the contract already; they become usable rules in P2-BE-06.
/// </summary>
public enum AssigneeRuleKind
{
    RequesterManager,
    Role,
    DepartmentRole,
    SpecificUser,
    ManagerOfManager,
}
