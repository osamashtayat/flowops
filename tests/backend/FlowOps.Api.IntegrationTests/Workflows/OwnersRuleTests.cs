using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Workflows;

/// <summary>
/// P2-BE-05 (TESTING.md §3.7): the owner's rule end to end, "Manager → Finance if amount &gt; 1000 JOD, else → Procurement",
/// with skipped steps, the planned route, version pinning and the route preview. I5, I11 and T3 keep their Phase 1 tests.
/// </summary>
public sealed class OwnersRuleTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string FinanceRule = "Only if amount is greater than 1,000.000 JOD";
    private static readonly Uri Purchase = new("/api/requests/purchase", UriKind.Relative);
    private static readonly Uri Preview = new("/api/requests/route-preview", UriKind.Relative);

    private readonly Dictionary<string, TestUser> _users = new(StringComparer.Ordinal);

    [Theory]
    [InlineData("400.000", false)]
    [InlineData("1000.000", false)]
    [InlineData("1500.000", true)]
    public async Task ManagerApproval_GoesToFinanceOnlyAbove1000(string amount, bool viaFinance)
    {
        await SeedOrgAsync();
        await PublishOwnersRuleAsync();
        var id = await SubmitAsync(amount);
        RouteOf(await DetailsAsync(id)).ShouldBe(viaFinance
            ? [("manager-approval", "Current"), ("finance-approval", "Planned"), ("procurement-fulfillment", "Planned")]
            : [("manager-approval", "Current"), ("procurement-fulfillment", "Planned")]);

        (await DecideAsync("karim", id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await DetailsAsync(id);
        var activity = await ActivityAsync(id);
        if (viaFinance)
        {
            (details.GetProperty("status").GetString(), StepKey(details)).ShouldBe(("InReview", "finance-approval"));
            activity.ShouldNotContain(a => a.GetProperty("type").GetString() == "StepSkipped");
            (await DecideAsync("fiona", id, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        else
        {
            (details.GetProperty("status").GetString(), StepKey(details)).ShouldBe(("Approved", "procurement-fulfillment"));
            RouteOf(details).ShouldBe([("manager-approval", "Done"), ("finance-approval", "Skipped"), ("procurement-fulfillment", "Current")]);
            var skip = activity.Single(a => a.GetProperty("type").GetString() == "StepSkipped");
            skip.GetProperty("actor").ValueKind.ShouldBe(JsonValueKind.Null);
            skip.GetProperty("step").GetProperty("key").GetString().ShouldBe("finance-approval");
            skip.GetProperty("body").GetString().ShouldBe(FinanceRule);
            (await OpenItemsAsync("fiona")).ShouldBeEmpty();
        }

        (await DecideAsync("pete", id, "fulfill")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DetailsAsync(id)).GetProperty("status").GetString().ShouldBe("Completed");
    }

    [Fact]
    public async Task ARequestSubmittedUnderV1_KeepsV1sRouteAfterV2IsPublished()
    {
        await SeedOrgAsync();
        var underV1 = await SubmitAsync("400.000");
        await PublishOwnersRuleAsync();
        var underV2 = await SubmitAsync("400.000");

        (await DecideAsync("karim", underV1, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DecideAsync("karim", underV2, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        StepKey(await DetailsAsync(underV1)).ShouldBe("finance-approval");
        StepKey(await DetailsAsync(underV2)).ShouldBe("procurement-fulfillment");
    }

    [Theory]
    [InlineData("400.000", "Skipped")]
    [InlineData("1000.000", "Skipped")]
    [InlineData("1000.001", "Planned")]
    public async Task RoutePreview_ShowsEveryStepPlannedOrSkipped_AndSavesNothing(string amount, string finance)
    {
        await SeedOrgAsync();
        await PublishOwnersRuleAsync();
        using var lina = await Factory.ClientFor(_users["lina"]);

        using var response = await lina.PostAsJsonAsync(Preview,
            new { type = "EquipmentPurchase", facts = new { amount = new { amount, currency = "JOD" }, leaveType = "Sick" } }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var steps = (await JsonBody.ReadAsync(response)).GetProperty("steps").EnumerateArray()
            .Select(s => (s.GetProperty("step").GetProperty("key").GetString(), s.GetProperty("state").GetString()));
        steps.ShouldBe([("manager-approval", "Planned"), ("finance-approval", finance), ("procurement-fulfillment", "Planned")]);
        await using var scope = Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Requests.CountAsync(Ct)).ShouldBe(0);
    }

    [Theory]
    [InlineData("EquipmentPurchase", "facts.amount")]
    [InlineData("Leave", "facts.leaveType")]
    public async Task RoutePreview_WithoutAFactOfTheType_Returns400(string type, string field)
    {
        await SeedOrgAsync();
        using var lina = await Factory.ClientFor(_users["lina"]);

        // Leave reads only leaveType and days: the amount sent here is ignored, not a substitute.
        using var response = await lina.PostAsJsonAsync(Preview,
            new { type, facts = new { amount = (object?)(type == "Leave" ? new { amount = "5.000", currency = "JOD" } : null), days = 3 } }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe([field]);
    }

    [Fact]
    public async Task RoutePreview_ForLeave_ReadsLeaveFacts()
    {
        await SeedOrgAsync();
        using var lina = await Factory.ClientFor(_users["lina"]);

        using var response = await lina.PostAsJsonAsync(Preview, new { type = "Leave", facts = new { leaveType = "Annual", days = 2 } }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(response)).GetProperty("steps").EnumerateArray()
            .Select(s => s.GetProperty("step").GetProperty("key").GetString()).ShouldBe(["manager-approval", "hr-confirmation"]);
    }

    private async Task SeedOrgAsync()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        _users["karim"] = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        _users["lina"] = await Given.User("lina").InDepartment(sales).ManagedBy(_users["karim"]).CreateAsync(Factory);
        _users["fiona"] = await Given.User("fiona").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);
        _users["pete"] = await Given.User("pete").WithRoles(Roles.ProcurementOfficer).CreateAsync(Factory);
        _users["ada"] = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
    }

    /// <summary>
    /// Publishes v2 of the purchase workflow with the owner's rule. Written through the domain directly until the admin
    /// API exists (P2-BE-07); the harness puts the seeded v1 back after the test.
    /// </summary>
    private async Task PublishOwnersRuleAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Factory.Clock.GetUtcNow();
        var purchase = await db.WorkflowDefinitions.SingleAsync(d => d.RequestType == RequestType.EquipmentPurchase, Ct);
        var v1 = await db.WorkflowVersions.SingleAsync(v => v.WorkflowId == purchase.Id && v.Status == WorkflowVersionStatus.Published, Ct);
        v1.Retire(now);
        await db.SaveChangesAsync(Ct);
        var v2 = WorkflowVersion.CreateDraft(purchase.Id, 2,
        [
            WorkflowStep.Create("manager-approval", "Manager approval", WorkTaskKind.Approval, AssigneeRule.RequesterManager),
            WorkflowStep.Create("finance-approval", "Finance approval", WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer),
                Condition.Create(FactKey.Amount, ConditionOperator.GreaterThan, ["1000.000"])),
            WorkflowStep.Create("procurement-fulfillment", "Procurement fulfilment", WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer)),
        ], _users["ada"].Id, now);
        v2.Publish(_users["ada"].Id, now);
        db.WorkflowVersions.Add(v2);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> SubmitAsync(string amount)
    {
        using var lina = await Factory.ClientFor(_users["lina"]);
        using var response = await lina.PostAsJsonAsync(Purchase, new
        {
            itemName = "Laptop", description = "14 inch", estimatedCost = new { amount, currency = "JOD" }, justification = "Broken",
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> DetailsAsync(Guid id)
    {
        using var lina = await Factory.ClientFor(_users["lina"]);
        return await JsonBody.ReadAsync(await lina.GetAsync(new Uri($"/api/requests/{id}", UriKind.Relative), Ct));
    }

    private async Task<List<JsonElement>> ActivityAsync(Guid id)
    {
        using var lina = await Factory.ClientFor(_users["lina"]);
        return [.. (await JsonBody.ReadAsync(await lina.GetAsync(new Uri($"/api/requests/{id}/activity", UriKind.Relative), Ct))).EnumerateArray()];
    }

    private async Task<List<JsonElement>> OpenItemsAsync(string user)
    {
        using var client = await Factory.ClientFor(_users[user]);
        using var response = await client.GetAsync(new Uri("/api/tasks?status=Open", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. (await JsonBody.ReadAsync(response)).GetProperty("items").EnumerateArray()];
    }

    private async Task<HttpResponseMessage> DecideAsync(string user, Guid requestId, string action)
    {
        var item = (await OpenItemsAsync(user)).Single(i => i.GetProperty("request").GetProperty("id").GetGuid() == requestId);
        using var client = await Factory.ClientFor(_users[user]);
        object body = action == "fulfill"
            ? new { version = item.GetProperty("version").GetString(), note = (string?)null }
            : new { version = item.GetProperty("version").GetString(), comment = (string?)null };
        return await client.PostAsJsonAsync(new Uri($"/api/tasks/{item.GetProperty("taskId").GetGuid()}/{action}", UriKind.Relative), body, Ct);
    }

    private static string? StepKey(JsonElement details) => details.GetProperty("step").GetProperty("key").GetString();

    private static (string?, string?)[] RouteOf(JsonElement details) =>
        [.. details.GetProperty("route").EnumerateArray()
            .Select(r => (r.GetProperty("step").GetProperty("key").GetString(), r.GetProperty("state").GetString()))];
}
