using System.Globalization;
using System.Threading.RateLimiting;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Errors;

/// <summary>The one 429 response for every rate limit: ProblemDetails with a <c>code</c> and a <c>Retry-After</c> header.</summary>
internal static class RateLimitRejection
{
    public static Task WriteAsync(HttpContext http, RateLimitLease lease, string code, string detail)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        http.Response.StatusCode = ErrorStatusCodes.For(code);
        var problem = new ProblemDetails { Status = http.Response.StatusCode, Detail = detail };
        problem.Extensions[ProblemDetailsSetup.CodeKey] = code;
        return http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem })
            .AsTask();
    }
}
