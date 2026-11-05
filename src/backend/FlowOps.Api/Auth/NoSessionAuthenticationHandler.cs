using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FlowOps.Api.Auth;

/// <summary>
/// Placeholder until BE-05 adds the Identity cookie: nobody is authenticated, so protected endpoints
/// answer 401 (and 403 for a failed policy) as ProblemDetails rather than failing with "no scheme".
/// </summary>
public sealed class NoSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "NoSession";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}
