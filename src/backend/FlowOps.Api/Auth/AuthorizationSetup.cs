using FlowOps.Domain.Organization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace FlowOps.Api.Auth;

public static class AuthorizationSetup
{
    public static IServiceCollection AddFlowOpsAuthorization(this IServiceCollection services)
    {
        services
            .AddAuthentication(NoSessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, NoSessionAuthenticationHandler>(NoSessionAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            // Authenticated by default; [AllowAnonymous] only on login and health (security rules).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Administrator))
            .AddPolicy(Policies.CanSubmit, p => p.RequireRole(Roles.Employee))
            .AddPolicy(Policies.TasksView, p => p.RequireRole(Roles.FinanceOfficer, Roles.HrOfficer, Roles.ProcurementOfficer));

        return services;
    }
}
