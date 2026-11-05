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

    [Fact]
    public void EveryUnsafeEndpoint_IsAnMvcAction()
    {
        var unsafeEndpoints = UnsafeEndpoints().ToList();

        unsafeEndpoints.ShouldNotBeEmpty();
        unsafeEndpoints.Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
            .Select(e => e.RoutePattern.RawText).ShouldBeEmpty();
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
