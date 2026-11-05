using Microsoft.AspNetCore.Identity;

namespace FlowOps.Infrastructure.Identity;

/// <summary>
/// The Identity user plus the FlowOps profile (DOMAIN_MODEL.md §2.2). Lives in Infrastructure so
/// Domain/Application never see Identity types (ADR-0002); they query it as <c>UserReadModel</c>.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";

    public Guid? DepartmentId { get; set; }

    /// <summary>Direct manager. Never the user themself (DB check) and never a cycle (BE-08 rules).</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>Inactive users cannot sign in, and their existing sessions end at the next stamp validation.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set when an administrator creates or resets the account; blocks everything but /api/auth/*.</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
