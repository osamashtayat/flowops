using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// The real Api on a real PostgreSQL (TESTING.md §2). One container per test collection
/// (<see cref="DatabaseCollectionDefinition"/>); migrations are applied once, and <see cref="ResetAsync"/>
/// empties the data between tests while keeping reference rows the app seeds (roles).
/// </summary>
public sealed class FlowOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Tables Respawn leaves alone: migration history and data the app seeds at start-up.</summary>
    private static readonly Table[] KeptTables = ["__EFMigrationsHistory", "roles"];

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner? _respawner;

    /// <summary>The app's <see cref="TimeProvider"/>. Rewound to <see cref="TestClock.Start"/> before every test.</summary>
    public TestClock Clock { get; } = new();

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

    /// <summary>Deletes all test data and rewinds the clock. <see cref="DatabaseTest"/> calls it before each test.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
        Clock.Reset();
    }

    /// <summary>An HTTPS client (the auth cookie is <c>__Host-</c>/Secure) that never follows redirects.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FlowOps", ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
