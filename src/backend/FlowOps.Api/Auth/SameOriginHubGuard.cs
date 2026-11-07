using FlowOps.Application.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Api.Auth;

/// <summary>
/// The hub takes no antiforgery token (browsers can't add a header to a WebSocket), and SameSite=Strict only stops
/// cross-<i>site</i> requests. So a browser request to <c>/hubs</c> must come from our own origin: an <c>Origin</c> whose
/// host differs from the request's host is refused with 403 (AUTHORIZATION.md §1.3). Requests without <c>Origin</c>
/// (non-browser clients) can't carry a victim's cookie cross-origin, so they pass to normal authentication.
/// </summary>
public sealed class SameOriginHubGuard(RequestDelegate next)
{
    public const string HubPathPrefix = "/hubs";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !IsSameHost(origin, context.Request.Host))
        {
            throw new ForbiddenException(ErrorCodes.AuthForbidden, "Cross-origin connections are not allowed.");
        }

        return next(context);
    }

    // Host and port only: TLS may end at a reverse proxy, so the scheme the API sees can differ from the browser's.
    private static bool IsSameHost(string origin, HostString host) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.Equals(uri.IsDefaultPort ? uri.Host : uri.Authority, host.Value, StringComparison.OrdinalIgnoreCase);
}
