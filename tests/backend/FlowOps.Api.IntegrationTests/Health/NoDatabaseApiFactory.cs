using FlowOps.Api.IntegrationTests.Harness;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowOps.Api.IntegrationTests.Health;

/// <summary>The real Api, isolated from the developer's machine (#69). Needs no database.</summary>
public sealed class NoDatabaseApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.IsolateFromDeveloperMachine();
}
