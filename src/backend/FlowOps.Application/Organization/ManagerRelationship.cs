using System.Linq.Expressions;
using FlowOps.Application.Common;

namespace FlowOps.Application.Organization;

/// <summary>"Manager" is derived, not a role (DOMAIN_MODEL.md §2.2): a user is a manager iff they have an active direct report.</summary>
public static class ManagerRelationship
{
    public static Expression<Func<UserReadModel, bool>> ActiveDirectReportOf(Guid managerId) =>
        u => u.ManagerId == managerId && u.IsActive;
}
