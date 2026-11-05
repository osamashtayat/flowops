using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FlowOps.Api.Auth;

/// <summary>
/// Global filter: every unsafe method needs a valid <c>X-XSRF-TOKEN</c> header (AUTHORIZATION.md §1.3),
/// except endpoints marked <c>[IgnoreAntiforgeryToken]</c> (login only). Failure → 403 <c>auth.forbidden</c>.
/// </summary>
public sealed class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            || HttpMethods.IsOptions(http.Request.Method) || HttpMethods.IsTrace(http.Request.Method))
        {
            return;
        }

        if (context.Filters.OfType<IgnoreAntiforgeryTokenAttribute>().Any())
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(http))
        {
            throw new ForbiddenException(ErrorCodes.AuthForbidden, "The request is missing a valid XSRF token.");
        }
    }
}

/// <summary>
/// Issues the readable <c>XSRF-TOKEN</c> cookie after a successful action (login, <c>/me</c>). The token is
/// bound to <c>HttpContext.User</c>, which login has already switched to the signed-in user.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IssueXsrfTokenAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        var http = context.HttpContext;
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        http.Response.Cookies.Append(AuthorizationSetup.XsrfTokenCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }
}
