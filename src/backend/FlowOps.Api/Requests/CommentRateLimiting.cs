using System.Security.Claims;
using System.Threading.RateLimiting;
using FlowOps.Api.Errors;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FlowOps.Api.Requests;

/// <summary>Limit for <c>POST /api/requests/{id}/comments</c> per signed-in user (#37). Bound from <see cref="SectionName"/>.</summary>
public sealed class CommentRateLimitOptions
{
    public const string SectionName = "FlowOps:CommentRateLimit";

    /// <summary>Far above a real conversation, low enough that one user can't flood a request's timeline and its deciders.</summary>
    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Fixed window per user id, in memory per instance like the login limits. Anonymous calls aren't limited here:
/// authorization answers them with 401. Over the limit → 429 <c>request.comment_rate_limited</c> + <c>Retry-After</c>.
/// </summary>
public sealed class CommentRateLimitPolicy(IOptions<CommentRateLimitOptions> options) : IRateLimiterPolicy<string>
{
    public const string Name = "comments-per-user";

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, _) => new ValueTask(
        RateLimitRejection.WriteAsync(context.HttpContext, context.Lease, ErrorCodes.RequestCommentRateLimited,
            "You're adding comments too quickly. Please wait a moment and try again."));

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return RateLimitPartition.GetNoLimiter("anonymous");
        }

        var limits = options.Value;
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limits.PermitLimit,
            Window = limits.Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }
}

public static class CommentRateLimiting
{
    public static IServiceCollection AddFlowOpsCommentRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // A zero limit or window would make every comment a 500 at runtime; fail at start-up instead.
        services.AddOptions<CommentRateLimitOptions>()
            .Bind(configuration.GetSection(CommentRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit > 0 && o.Window > TimeSpan.Zero, "FlowOps:CommentRateLimit needs a positive PermitLimit and Window.")
            .ValidateOnStart();
        services.AddRateLimiter(options => options.AddPolicy<string, CommentRateLimitPolicy>(CommentRateLimitPolicy.Name));
        return services;
    }
}
