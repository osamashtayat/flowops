using Microsoft.AspNetCore.Identity;

namespace FlowOps.Infrastructure.Identity;

/// <summary>
/// The Identity user. Lives in Infrastructure so Domain/Application never see Identity types (ADR-0002);
/// they refer to users by id. The FlowOps profile fields (DOMAIN_MODEL.md §2.2) are mapped with the
/// rest of the domain in BE-07.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
