using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace FlowOps.Api.IntegrationTests.Workflows;

/// <summary>
/// P2-BE-03 (TESTING.md §3.7): <c>AddWorkflowDefinitions</c> on a database that already holds requests in every state pins
/// each one to its type's v1, and the aggregates still load and advance. Runs in its own database on the shared container.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class BackfillMigrationTests(FlowOpsApiFactory factory)
{
    private const string MigrationBefore = "20261111162110_AddStepSkippedActivity";

    /// <summary>Every (type, status, stage) a Phase 1 request can be in.</summary>
    private static readonly (RequestType Type, RequestStatus Status, RequestStage Stage)[] EveryState =
    [
        (RequestType.EquipmentPurchase, RequestStatus.InReview, RequestStage.ManagerApproval),
        (RequestType.EquipmentPurchase, RequestStatus.InReview, RequestStage.FinanceApproval),
        (RequestType.EquipmentPurchase, RequestStatus.Approved, RequestStage.ProcurementFulfillment),
        (RequestType.EquipmentPurchase, RequestStatus.Completed, RequestStage.Closed),
        (RequestType.EquipmentPurchase, RequestStatus.Rejected, RequestStage.Closed),
        (RequestType.Leave, RequestStatus.InReview, RequestStage.ManagerApproval),
        (RequestType.Leave, RequestStatus.InReview, RequestStage.HrConfirmation),
        (RequestType.Leave, RequestStatus.Completed, RequestStage.Closed),
        (RequestType.Leave, RequestStatus.Rejected, RequestStage.Closed),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_PinsEveryExistingRequestToItsTypesV1()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = NewContext(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, Ct);
        var ids = await InsertPhaseOneRequestsAsync(connectionString);

        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var v1 = await db.WorkflowVersions.AsNoTracking()
            .Join(db.WorkflowDefinitions, v => v.WorkflowId, d => d.Id, (v, d) => new { d.RequestType, v.Id, v.Number })
            .ToDictionaryAsync(x => x.RequestType, x => (x.Id, x.Number), Ct);
        var requests = await db.Requests.AsNoTracking().Include(r => r.Tasks).ToListAsync(Ct);
        requests.Count.ShouldBe(EveryState.Length);
        foreach (var request in requests)
        {
            request.WorkflowVersionId.ShouldBe(v1[request.Type].Id);
            (request.Type, request.Status, request.Stage).ShouldBe(EveryState[ids.IndexOf(request.Id)]);
        }

        v1.Values.ShouldAllBe(v => v.Number == 1);
    }

    [Fact]
    public async Task Migration_LeavesOpenRequestsAbleToAdvanceOnV1()
    {
        var connectionString = await CreateDatabaseAsync();
        await using var db = NewContext(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(MigrationBefore, Ct);
        var ids = await InsertPhaseOneRequestsAsync(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        // The purchase at finance: approving it opens procurement, as Phase 1 did.
        var atFinance = ids[1];
        var request = await db.Requests.Include(r => r.Tasks).Include(r => r.Activities).SingleAsync(r => r.Id == atFinance, Ct);
        var v1 = await db.WorkflowVersions.AsNoTracking().SingleAsync(v => v.Id == request.WorkflowVersionId, Ct);
        request.DecideTask(request.OpenTask!.Id, Guid.CreateVersion7(), TaskDecision.Approve(null), v1,
            step => Assignee.ForRole(step.Assignee.Role!), DateTimeOffset.UtcNow);

        (request.Status, request.Stage).ShouldBe((RequestStatus.Approved, RequestStage.ProcurementFulfillment));
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"backfill_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(factory.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync(Ct);
        }

        return new NpgsqlConnectionStringBuilder(factory.ConnectionString) { Database = name }.ConnectionString;
    }

    private static AppDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>
    /// Requests (with an open task where the state has one) written as Phase 1 rows. Users and departments don't matter
    /// to the backfill, so foreign keys are switched off for this session only.
    /// </summary>
    private static async Task<List<Guid>> InsertPhaseOneRequestsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using (var replica = new NpgsqlCommand("SET session_replication_role = replica", connection))
        {
            await replica.ExecuteNonQueryAsync(Ct);
        }

        var ids = new List<Guid>();
        for (var i = 0; i < EveryState.Length; i++)
        {
            var (type, status, stage) = EveryState[i];
            var id = Guid.CreateVersion7();
            ids.Add(id);
            var closed = stage == RequestStage.Closed;
            await using var insert = new NpgsqlCommand("""
                INSERT INTO requests (id, number, type, requester_id, department_id, status, stage, submitted_at, closed_at)
                VALUES (@id, @number, @type, gen_random_uuid(), gen_random_uuid(), @status, @stage, now(), CASE WHEN @closed THEN now() END);
                INSERT INTO work_tasks (id, request_id, kind, stage, assignee_user_id, assignee_role, status, created_at)
                SELECT gen_random_uuid(), @id, @kind, @stage, NULL, @role, 'Open', now() WHERE NOT @closed;
                """, connection);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("number", $"FO-2026-{i + 1:000000}");
            insert.Parameters.AddWithValue("type", type.ToString());
            insert.Parameters.AddWithValue("status", status.ToString());
            insert.Parameters.AddWithValue("stage", stage.ToString());
            insert.Parameters.AddWithValue("closed", closed);
            insert.Parameters.AddWithValue("kind", (stage == RequestStage.ProcurementFulfillment ? WorkTaskKind.Fulfillment : WorkTaskKind.Approval).ToString());
            insert.Parameters.AddWithValue("role", RoleFor(stage));
            await insert.ExecuteNonQueryAsync(Ct);
        }

        return ids;
    }

    /// <summary>Open tasks in the backfill data sit in a role queue, so they need no user.</summary>
    private static string RoleFor(RequestStage stage) => stage switch
    {
        RequestStage.FinanceApproval => "FinanceOfficer",
        RequestStage.HrConfirmation => "HrOfficer",
        RequestStage.ProcurementFulfillment => "ProcurementOfficer",
        _ => "Employee",
    };
}
