using System.Threading.RateLimiting;
using FlowOps.Api.Errors;
using FlowOps.Application.Organization;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace FlowOps.Api.Auth;

/// <summary>Limits for <c>POST /api/auth/login</c> (AUTHORIZATION.md §1.6). Bound from <see cref="SectionName"/>.</summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "FlowOps:LoginRateLimit";

    /// <summary>Generous: a whole office signs in from one NAT address at 09:00.</summary>
    public int PerIpPermitLimit { get; set; } = 30;

    public TimeSpan PerIpWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Counts every attempt for an email, known or not, so the limit itself reveals nothing.</summary>
    public int PerEmailPermitLimit { get; set; } = 10;

    public TimeSpan PerEmailWindow { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// ASP.NET Core rate limiting on login: per client IP in the rate-limiter middleware (rejected before the body
/// is read), per email in <see cref="LoginEmailRateLimitFilter"/> (the email is only known after binding).
/// Both reject with 429 <c>auth.rate_limited</c> and <c>Retry-After</c>.
/// </summary>
public static class LoginRateLimiting
{
    public const string PerIpPolicy = "login-per-ip";

    public static IServiceCollection AddFlowOpsLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LoginRateLimitOptions>()
            .Bind(configuration.GetSection(LoginRateLimitOptions.SectionName))
            .Validate(o => o.PerIpPermitLimit > 0 && o.PerIpWindow > TimeSpan.Zero && o.PerEmailPermitLimit > 0 && o.PerEmailWindow > TimeSpan.Zero,
                "FlowOps:LoginRateLimit needs positive limits and windows.")
            .ValidateOnStart();
        services.AddSingleton<LoginEmailRateLimitFilter>();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PerIpPolicy, http =>
            {
                var limits = http.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
                // Behind a reverse proxy this is the proxy's address unless forwarded headers are enabled, as the Docker stack does (OPS-01).
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => Window(limits.PerIpPermitLimit, limits.PerIpWindow));
            });
            // Global fallback, used only by policies without their own OnRejected: today that's just the login per-IP
            // policy. A new policy must bring its own (see CommentRateLimitPolicy), or it would answer auth.rate_limited.
            options.OnRejected = (context, _) => new ValueTask(WriteRejectionAsync(context.HttpContext, context.Lease));
        });
        return services;
    }

    internal static FixedWindowRateLimiterOptions Window(int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    internal static Task WriteRejectionAsync(HttpContext http, RateLimitLease lease) =>
        RateLimitRejection.WriteAsync(http, lease, ErrorCodes.AuthRateLimited, "Too many sign-in attempts. Please wait a few minutes and try again.");
}

/// <summary>Per-email login limit. Singleton: it owns the in-memory limiter for the app's lifetime.</summary>
public sealed class LoginEmailRateLimitFilter : IAsyncActionFilter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginEmailRateLimitFilter(IOptions<LoginRateLimitOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.Value;
        _limiter = PartitionedRateLimiter.Create<string, string>(email =>
            RateLimitPartition.GetFixedWindowLimiter(email, _ => LoginRateLimiting.Window(limits.PerEmailPermitLimit, limits.PerEmailWindow)));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        // [ApiController] has already answered 400 for an invalid body, so a bound request is here.
        var login = context.ActionArguments.Values.OfType<LoginRequest>().Single();
        using var lease = _limiter.AttemptAcquire(login.Email.Trim().ToUpperInvariant());
        if (!lease.IsAcquired)
        {
            await LoginRateLimiting.WriteRejectionAsync(context.HttpContext, lease);
            context.Result = new EmptyResult();
            return;
        }

        await next();
    }

    public void Dispose() => _limiter.Dispose();
}
