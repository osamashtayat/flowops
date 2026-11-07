using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Contract;

/// <summary>
/// XSRF is enforced by an MVC filter, so every unsafe endpoint must be an MVC action, and only login may opt out
/// (AUTHORIZATION.md §1.3). A non-MVC unsafe endpoint (e.g. a hub) must add its own check and an entry here.
/// </summary>
public sealed class AntiforgeryCoverageTests(ContractApiFactory factory) : IClassFixture<ContractApiFactory>
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    /// <summary>
    /// The SignalR hub (BE-14, AUTHORIZATION.md §1.3): negotiate and the long-polling send are POSTs outside MVC. They take
    /// no XSRF header (browsers can't add one to a WebSocket), which is safe because the hub has no client-callable methods,
    /// only ever sends id hints to the signed-in user themselves, and the session cookie is SameSite=Strict.
    /// </summary>
    private static readonly string[] HubEndpoints = ["/hubs/notifications/negotiate", "/hubs/notifications"];

    [Fact]
    public void EveryUnsafeEndpoint_IsAnMvcAction()
    {
        var unsafeEndpoints = UnsafeEndpoints().ToList();

        unsafeEndpoints.ShouldNotBeEmpty();
        unsafeEndpoints.Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
            .Select(e => e.RoutePattern.RawText).Order(StringComparer.Ordinal).ShouldBe(HubEndpoints.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OnlyLogin_IgnoresAntiforgery()
    {
        UnsafeEndpoints()
            .Where(e => e.Metadata.GetMetadata<IgnoreAntiforgeryTokenAttribute>() is not null)
            .Select(e => e.RoutePattern.RawText)
            .ShouldBe(["api/auth/login"]);
    }

    private IEnumerable<RouteEndpoint> UnsafeEndpoints() =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Any(m => !SafeMethods.Contains(m)) ?? true);
}
