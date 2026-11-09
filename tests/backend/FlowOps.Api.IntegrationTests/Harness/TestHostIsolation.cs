using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// Keeps every test host off the developer's machine state (#69). <c>WebApplicationFactory</c> runs the Api in Development,
/// which loads the developer's user-secrets: a connection string there pointed hosts at the local dev database, and a
/// bootstrap admin there made start-up create an administrator in it. Tests could also pass locally only because of them.
/// </summary>
public static class TestHostIsolation
{
    /// <summary>For hosts that need no database: constructible DbContext, never opened.</summary>
    public const string UnusedConnectionString = "Host=no-database.invalid;Database=unused";

    /// <summary>
    /// Drops the user-secrets source and pins what it could have supplied: the connection string (a test database, or one
    /// that is never opened) and an empty bootstrap admin. Tests that need other values set them on top.
    /// </summary>
    public static IWebHostBuilder IsolateFromDeveloperMachine(this IWebHostBuilder builder, string connectionString = UnusedConnectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .ConfigureAppConfiguration((_, config) =>
            {
                foreach (var source in config.Sources.Where(IsUserSecrets).ToList())
                {
                    config.Sources.Remove(source);
                }
            })
            .UseSetting("ConnectionStrings:FlowOps", connectionString)
            .UseSetting("FlowOps:BootstrapAdmin:Email", "")
            .UseSetting("FlowOps:BootstrapAdmin:Password", "");
    }

    /// <summary>How <c>AddUserSecrets</c> registers itself: a JSON source for <c>secrets.json</c> in the user-secrets folder.</summary>
    public static bool IsUserSecrets(IConfigurationSource source) =>
        source is JsonConfigurationSource { Path: "secrets.json" };
}
