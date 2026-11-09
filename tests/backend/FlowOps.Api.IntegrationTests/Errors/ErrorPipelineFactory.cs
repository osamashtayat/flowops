using FlowOps.Api.IntegrationTests.Harness;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Errors;

/// <summary>The real Api plus <see cref="ThrowingController"/>. Needs no database.</summary>
public sealed class ErrorPipelineFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.IsolateFromDeveloperMachine().ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly));
}
