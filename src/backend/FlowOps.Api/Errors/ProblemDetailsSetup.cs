using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowOps.Api.Errors;

/// <summary>
/// Gives every ProblemDetails response, ours or the framework's, the contract shape:
/// <c>type</c> from the code, <c>title</c>, <c>code</c> and <c>traceId</c> (API_CONTRACT.md §2).
/// </summary>
public static class ProblemDetailsSetup
{
    public const string CodeKey = "code";
    public const string TraceIdKey = "traceId";
    public const string TypeBaseUri = "https://flowops.local/problems/";

    public static IServiceCollection AddFlowOpsProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        return services;
    }

    private static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;

        if (problem.Extensions.TryGetValue(CodeKey, out var existing) is false || existing is not string)
        {
            problem.Extensions[CodeKey] = ErrorStatusCodes.DefaultCodeFor(status);
        }

        var code = (string)problem.Extensions[CodeKey]!;
        problem.Type = TypeBaseUri + code;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(status);
        problem.Instance = null;
        problem.Extensions.Remove("exception");
        problem.Extensions[TraceIdKey] =
            Activity.Current?.Id
            ?? context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id
            ?? context.HttpContext.TraceIdentifier;
    }
}
