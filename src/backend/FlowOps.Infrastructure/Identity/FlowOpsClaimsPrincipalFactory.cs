using System.Security.Claims;
using FlowOps.Application.Common;
using FlowOps.Application.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowOps.Infrastructure.Identity;

/// <summary>
/// Builds the session principal: Identity's id/name/stamp/role claims plus the FlowOps flags the Api's
/// policies and password-change gate read (<see cref="AppClaimTypes"/>). Re-run on every stamp validation.
/// </summary>
public sealed class FlowOpsClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options,
    AppDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(AppClaimTypes.MustChangePassword, AppClaimTypes.True));
        }

        if (await ((IAppDbContext)db).Users.AnyAsync(ManagerRelationship.ActiveDirectReportOf(user.Id)))
        {
            identity.AddClaim(new Claim(AppClaimTypes.IsManager, AppClaimTypes.True));
        }

        return identity;
    }
}
