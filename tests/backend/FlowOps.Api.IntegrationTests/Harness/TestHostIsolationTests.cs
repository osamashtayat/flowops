using FlowOps.Api.IntegrationTests.Contract;
using FlowOps.Api.IntegrationTests.Errors;
using FlowOps.Api.IntegrationTests.Health;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>#69: no test host reads the developer's user-secrets or can reach their local dev database.</summary>
public sealed class TestHostIsolationTests
{
    public static TheoryData<string> NoDatabaseFactories() => ["health", "error-pipeline", "contract"];

    [Theory]
    [MemberData(nameof(NoDatabaseFactories))]
    public async Task NoDatabaseHost_HasNoUserSecretsAndAnUnreachableDatabase(string name)
    {
        await using var factory = Create(name);

        var config = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();

        AssertNoUserSecrets(config);
        config.GetConnectionString("FlowOps").ShouldBe(TestHostIsolation.UnusedConnectionString);
        config["FlowOps:BootstrapAdmin:Email"].ShouldBeNullOrEmpty();
        config["FlowOps:BootstrapAdmin:Password"].ShouldBeNullOrEmpty();
    }

    internal static void AssertNoUserSecrets(IConfigurationRoot config) =>
        config.Providers.OfType<FileConfigurationProvider>().ShouldNotContain(p => TestHostIsolation.IsUserSecrets(p.Source));

    private static WebApplicationFactory<Program> Create(string name) => name switch
    {
        "health" => new NoDatabaseApiFactory(),
        "error-pipeline" => new ErrorPipelineFactory(),
        "contract" => new ContractApiFactory(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };
}
