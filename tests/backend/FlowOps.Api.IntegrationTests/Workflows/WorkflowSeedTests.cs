using System.Net;
using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Application.Requests;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowOps.Api.IntegrationTests.Workflows;

/// <summary>P2-BE-03 (TESTING.md §3.7): the migration seeds purchase and leave as Published v1, equal to Phase 1's fixed steps.</summary>
public sealed class WorkflowSeedTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    /// <summary>Phase 1's <c>FixedProcesses</c>, as it was before P2-BE-03 deleted it, with the v1 keys of DOMAIN_MODEL.md §7.4.</summary>
    public static readonly TheoryData<RequestType, (string Key, WorkTaskKind Kind, AssigneeRuleKind Rule, string? Role)[]> PhaseOneSteps = new()
    {
        {
            RequestType.EquipmentPurchase,
            [
                ("manager-approval", WorkTaskKind.Approval, AssigneeRuleKind.RequesterManager, null),
                ("finance-approval", WorkTaskKind.Approval, AssigneeRuleKind.Role, Roles.FinanceOfficer),
                ("procurement-fulfillment", WorkTaskKind.Fulfillment, AssigneeRuleKind.Role, Roles.ProcurementOfficer),
            ]
        },
        {
            RequestType.Leave,
            [
                ("manager-approval", WorkTaskKind.Approval, AssigneeRuleKind.RequesterManager, null),
                ("hr-confirmation", WorkTaskKind.Approval, AssigneeRuleKind.Role, Roles.HrOfficer),
            ]
        },
    };

    [Theory]
    [MemberData(nameof(PhaseOneSteps))]
    public async Task PublishedV1_HasThePhaseOneSteps(RequestType type, (string Key, WorkTaskKind Kind, AssigneeRuleKind Rule, string? Role)[] expected)
    {
        var v1 = await Factory.PublishedWorkflowAsync(type);

        (v1.Number, v1.Status).ShouldBe((1, WorkflowVersionStatus.Published));
        v1.PublishedAt.ShouldNotBeNull();
        v1.Steps.Select(s => (s.Key, s.Kind, s.Assignee.Kind, s.Assignee.Role)).ShouldBe(expected);
        v1.Steps.Select(s => s.Order).ShouldBe(Enumerable.Range(0, expected.Length));
        // Names equal what the API has shown for each stage since P2-DOC-01.
        v1.Steps.ShouldAllBe(s => s.Name == StepRef.ForTask(s.Stage).Name);
    }

    [Fact]
    public async Task EveryRequestType_HasOneWorkflowWithExactlyOnePublishedVersion()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var published = await db.WorkflowDefinitions.AsNoTracking()
            .Select(d => new { d.RequestType, Published = db.WorkflowVersions.Count(v => v.WorkflowId == d.Id && v.Status == WorkflowVersionStatus.Published) })
            .ToListAsync(Ct);

        published.Select(p => p.RequestType).ShouldBe(Enum.GetValues<RequestType>(), ignoreOrder: true);
        published.ShouldAllBe(p => p.Published == 1);
    }

    [Theory]
    [InlineData("Published", "ux_workflow_versions_published")]
    [InlineData("Draft", "ux_workflow_versions_draft")]
    public async Task ASecondPublishedOrDraftVersion_IsRejectedByTheDatabase(string status, string index)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync(Ct);
        // Rolled back either way: the workflow tables survive Respawn resets.
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using var insert = new NpgsqlCommand($"""
            INSERT INTO workflow_versions (id, workflow_id, number, status, created_at, published_at)
            SELECT gen_random_uuid(), d.id, n, '{status}', now(), CASE WHEN '{status}' = 'Draft' THEN NULL ELSE now() END
            FROM workflow_definitions d CROSS JOIN (VALUES (2), (3)) AS v(n) WHERE d.request_type = 'Leave'
            """, connection, transaction);

        var error = await Should.ThrowAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(Ct));

        (error.SqlState, error.ConstraintName).ShouldBe((PostgresErrorCodes.UniqueViolation, index));
    }

    [Theory]
    [InlineData(RequestType.EquipmentPurchase, "/api/requests/purchase")]
    [InlineData(RequestType.Leave, "/api/requests/leave")]
    public async Task Submit_PinsTheRequestToThePublishedVersion(RequestType type, string path)
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        var lina = await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        object body = type == RequestType.Leave
            ? new { startDate = "2026-03-10", endDate = "2026-03-12", leaveType = "Annual", reason = (string?)null }
            : new { itemName = "Laptop", description = "14 inch", estimatedCost = new { amount = "1250.5", currency = "JOD" }, justification = "Broken" };

        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var id = (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid();
        await using var scope = Factory.Services.CreateAsyncScope();
        var pinned = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Requests
            .Where(r => r.Id == id).Select(r => r.WorkflowVersionId).SingleAsync(Ct);
        pinned.ShouldBe((await Factory.PublishedWorkflowAsync(type)).Id);
    }
}
