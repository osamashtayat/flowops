using System.Net;
using System.Text.Json;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>Proves the harness itself (BE-04): real PostgreSQL, migrations applied, reset between tests, fake clock.</summary>
public sealed class HarnessTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    [Fact]
    public async Task GetHealth_OnRealDatabaseHost_Returns200()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        json.RootElement.GetProperty("status").GetString().ShouldBe("ok");
    }

    [Fact]
    public async Task Database_HasEveryMigrationApplied()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Reset_DeletesDataButKeepsRoles()
    {
        await ExecuteAsync("""
            INSERT INTO roles (id, name, normalized_name) VALUES ('01900000-0000-7000-8000-000000000001', 'HarnessRole', 'HARNESSROLE');
            INSERT INTO users (id, user_name, normalized_user_name, email_confirmed, phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES ('01900000-0000-7000-8000-000000000002', 'harness', 'HARNESS', false, false, false, false, 0);
            """);

        await Factory.ResetAsync();

        (await CountAsync("users")).ShouldBe(0);
        (await CountAsync("roles WHERE name = 'HarnessRole'")).ShouldBe(1);
        await ExecuteAsync("DELETE FROM roles WHERE name = 'HarnessRole'");
    }

    [Fact]
    public async Task Clock_IsRewoundToStartBeforeEachTest()
    {
        Factory.Clock.GetUtcNow().ShouldBe(TestClock.Start);
        Factory.Clock.Advance(TimeSpan.FromDays(3));

        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().ShouldBe(TestClock.Start.AddDays(3));

        await Factory.ResetAsync();
        Factory.Clock.GetUtcNow().ShouldBe(TestClock.Start);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<long> CountAsync(string fromClause)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {fromClause}", connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
