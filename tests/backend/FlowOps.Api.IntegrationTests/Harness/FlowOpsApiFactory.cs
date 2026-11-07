using System.Net.Http.Json;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// The real Api on a real PostgreSQL (TESTING.md §2). One container per test collection
/// (<see cref="DatabaseCollectionDefinition"/>); migrations are applied once, and <see cref="ResetAsync"/>
/// empties the data (and captured logs) between tests while keeping reference rows the app seeds (roles).
/// </summary>
public sealed class FlowOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Tables Respawn leaves alone: migration history and data the app seeds at start-up.</summary>
    private static readonly Table[] KeptTables = ["__EFMigrationsHistory", "roles"];

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner? _respawner;

    /// <summary>The app's <see cref="TimeProvider"/>. Rewound to <see cref="TestClock.Start"/> before every test.</summary>
    public TestClock Clock { get; } = new();

    /// <summary>Everything the app logged since the last <see cref="ResetAsync"/>.</summary>
    public LogSink Logs { get; } = new();

    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = KeptTables,
        });
    }

    /// <summary>Deletes all test data, restarts the request-number sequence and rewinds the clock. <see cref="DatabaseTest"/> calls it before each test.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        // Respawn empties tables but not sequences; restart it so request numbers are deterministic per test.
        await using (var restart = new NpgsqlCommand($"ALTER SEQUENCE {AppDbContext.RequestNumberSequence} RESTART", connection))
        {
            await restart.ExecuteNonQueryAsync();
        }

        Clock.Reset();
        Logs.Clear();
    }

    /// <summary>An HTTPS client (the auth cookie is <c>__Host-</c>/Secure) that never follows redirects.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>
    /// A client signed in as <paramref name="user"/> through the real login endpoint, sending the
    /// <c>X-XSRF-TOKEN</c> header the SPA would send (TESTING.md §2).
    /// </summary>
    public async Task<HttpClient> ClientFor(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var client = CreateHttpsClient();
        using var response = await LoginAsync(client, user.Email, user.Password);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", XsrfTokenFrom(response));
        return client;
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, password }, TestContext.Current.CancellationToken);

    /// <summary>The value of the readable <c>XSRF-TOKEN</c> cookie set by <paramref name="response"/>.</summary>
    public static string XsrfTokenFrom(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FlowOps", ConnectionString);
        // Development loads the developer's user-secrets; a configured bootstrap admin would seed before migrations.
        // BootstrapAdminTests sets its own values on top.
        builder.UseSetting("FlowOps:BootstrapAdmin:Email", "");
        builder.UseSetting("FlowOps:BootstrapAdmin:Password", "");
        // The whole suite signs in from one address with the same few emails; LoginRateLimitTests sets real limits.
        builder.UseSetting("FlowOps:LoginRateLimit:PerIpPermitLimit", "100000");
        builder.UseSetting("FlowOps:LoginRateLimit:PerEmailPermitLimit", "100000");
        builder.UseSetting("FlowOps:CommentRateLimit:PermitLimit", "100000"); // CommentRateLimitTests sets a real limit
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
