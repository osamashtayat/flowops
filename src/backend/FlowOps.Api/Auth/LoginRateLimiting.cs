using System.Globalization;
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
        services.Configure<LoginRateLimitOptions>(configuration.GetSection(LoginRateLimitOptions.SectionName));
        services.AddSingleton<LoginEmailRateLimitFilter>();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PerIpPolicy, http =>
            {
                var limits = http.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
                // Behind a reverse proxy this is the proxy's address until forwarded headers are configured (OPS-01).
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => Window(limits.PerIpPermitLimit, limits.PerIpWindow));
            });
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

    internal static Task WriteRejectionAsync(HttpContext http, RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Too many sign-in attempts. Please wait a few minutes and try again.",
        };
        problem.Extensions[ProblemDetailsSetup.CodeKey] = ErrorCodes.AuthRateLimited;
        return http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem })
            .AsTask();
    }
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
