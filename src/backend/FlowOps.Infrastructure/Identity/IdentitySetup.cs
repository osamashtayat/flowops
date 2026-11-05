using FlowOps.Application.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity with the rules of AUTHORIZATION.md §1.6. Cookie settings are the Api's (ADR-0003).</summary>
internal static class IdentitySetup
{
    /// <summary>
    /// How stale a session's roles, manager flag and must-change flag may get, and how long a deactivated
    /// or password-changed account's other sessions survive. Re-validation costs a few small queries.
    /// </summary>
    public static readonly TimeSpan SessionRevalidationInterval = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddFlowOpsIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = UserLimits.PasswordMinLength;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<FlowOpsSignInManager>()
            .AddClaimsPrincipalFactory<FlowOpsClaimsPrincipalFactory>();

        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = SessionRevalidationInterval);
        services.AddScoped<IIdentitySignIn, IdentitySignIn>();

        services.Configure<BootstrapAdminOptions>(configuration.GetSection(BootstrapAdminOptions.Section));
        services.AddHostedService<BootstrapAdminSeeder>();
        return services;
    }
}
