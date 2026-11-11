using FlowOps.Application.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FlowOps.Api.Auth;

/// <summary>Session cookie, antiforgery and coarse policies (AUTHORIZATION.md §1–2, §6; ADR-0003).</summary>
public static class AuthorizationSetup
{
    public const string SessionCookieName = "__Host-flowops";
    public const string AntiforgeryCookieName = "__Host-flowops-af";
    public const string XsrfHeaderName = "X-XSRF-TOKEN";

    /// <summary>Readable by the SPA, which echoes it in <see cref="XsrfHeaderName"/> (AUTHORIZATION.md §1.3).</summary>
    public const string XsrfTokenCookieName = "XSRF-TOKEN";

    public static IServiceCollection AddFlowOpsAuthorization(this IServiceCollection services)
    {
        // Identity (registered by Infrastructure) owns the scheme; the Api owns how its cookie behaves.
        // PostConfigure: AddIdentity replaces the cookie's Events object when its own configuration runs.
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            // An API never redirects to a login page: the status-code pages turn these into ProblemDetails.
            options.Events.OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden);
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = XsrfHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        services.AddAuthorizationBuilder()
            // Authenticated by default; [AllowAnonymous] only on login and health (security rules).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, p => p.RequireAssertion(c => c.User.HasCapability(Capability.AdminManage)))
            .AddPolicy(Policies.ManageWorkflows, p => p.RequireAssertion(c => c.User.HasCapability(Capability.WorkflowsManage)))
            .AddPolicy(Policies.CanSubmit, p => p.RequireAssertion(c => c.User.HasCapability(Capability.RequestsSubmit)))
            .AddPolicy(Policies.TasksView, p => p.RequireAssertion(c => c.User.HasCapability(Capability.TasksView)));

        return services;
    }

    private static Task SetStatus(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}
