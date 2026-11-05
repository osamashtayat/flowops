using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Errors;

/// <summary>
/// Maps typed exceptions to RFC 9457 ProblemDetails (ARCHITECTURE.md §6). Known failures carry their
/// code and message as <c>detail</c>; anything else becomes a 500 <c>server.error</c> with no internals.
/// </summary>
public sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (code, detail) = exception switch
        {
            DomainException e => (e.Code, e.Message),
            AppException e => (e.Code, e.Message),
            _ => (ErrorCodes.ServerError, "An unexpected error occurred."),
        };
        var status = ErrorStatusCodes.For(code);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            // Includes exceptions carrying a code the table doesn't know: the client sees server.error, the log the real code.
            LogUnhandled(logger, exception, code);
            code = ErrorCodes.ServerError;
        }
        else
        {
            // Messages may echo user input, so only the code is logged.
            LogHandled(logger, code, status);
        }

        httpContext.Response.StatusCode = status;
        // Field errors keep the shape of automatic model validation (API_CONTRACT.md §2).
        var problem = exception switch
        {
            ValidationFailedException validation =>
                new HttpValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal)),
            DomainException { Field: { } field } domain when status < StatusCodes.Status500InternalServerError =>
                new HttpValidationProblemDetails(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [domain.Message] }),
            _ => new ProblemDetails(),
        };
        problem.Status = status;
        problem.Detail = status >= StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : detail;
        problem.Extensions[ProblemDetailsSetup.CodeKey] = code;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception mapped to {Code}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string code);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request failed with {Code} ({Status})")]
    private static partial void LogHandled(ILogger logger, string code, int status);
}
