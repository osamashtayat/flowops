using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Access;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>
/// BE-11 over the <see cref="AccessOrg"/> requests: lists, details (reason masking, availableActions), activity and
/// comments, through HTTP (TESTING.md §3.3). Requests past the manager step are seeded through the domain until the
/// decision endpoints exist (BE-12).
/// </summary>
public sealed class RequestQueryTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    [Fact]
    public async Task ListMine_ReturnsOwnRequestsNewestFirst()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await SpreadSubmissionTimesAsync(org);
        using var lina = await ClientAsync(org, "lina");

        var page = await ListAsync(lina, "");

        Names(org, page).ShouldBe(["L1", "P3", "P2", "P1"]);
        page.GetProperty("totalCount").GetInt32().ShouldBe(4);
        var first = page.GetProperty("items")[0];
        first.GetProperty("title").GetString().ShouldBe("Annual leave · 10–12 Mar");
        first.GetProperty("requester").GetProperty("displayName").GetString().ShouldBe("Lina");
        first.GetProperty("department").GetProperty("name").GetString().ShouldBe("Sales");
    }

    [Theory]
    [InlineData("karim", "mine", new string[0])]
    [InlineData("karim", "visible", new[] { "L1", "P1", "P2", "P3", "PF" })]
    [InlineData("fiona", "visible", new[] { "P2", "P3", "PF" })]
    [InlineData("hana", "visible", new[] { "L1" })]
    [InlineData("boss", "visible", new string[0])]
    [InlineData("ada", "visible", new string[0])]
    public async Task List_ScopeFollowsVisibility(string user, string scope, string[] expected)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user);

        var page = await ListAsync(client, "scope=" + scope);

        Names(org, page).Order(StringComparer.Ordinal).ShouldBe(expected);
    }

    [Theory]
    [InlineData("status=Approved", new[] { "P3" })]
    [InlineData("type=Leave", new[] { "L1" })]
    [InlineData("search=fo-2026-000004", new[] { "L1" })]
    [InlineData("search=FO-2026-00000", new[] { "L1", "P1", "P2", "P3", "PF" })]
    [InlineData("search=LAPT", new[] { "P1", "P2", "P3", "PF" })]
    [InlineData("search=%25", new string[0])]
    [InlineData("search=2026-000004", new string[0])]
    public async Task ListVisible_Filters(string query, string[] expected)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var karim = await ClientAsync(org, "karim");

        var page = await ListAsync(karim, "scope=visible&" + query);

        Names(org, page).Order(StringComparer.Ordinal).ShouldBe(expected);
    }

    [Fact]
    public async Task List_Pages()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await SpreadSubmissionTimesAsync(org);
        using var karim = await ClientAsync(org, "karim");

        var page = await ListAsync(karim, "scope=visible&page=2&pageSize=2");

        page.GetProperty("totalCount").GetInt32().ShouldBe(5);
        Names(org, page).ShouldBe(["P3", "P2"]);
    }

    [Theory]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("scope=team", "scope")]
    [InlineData("status=Open", "status")]
    public async Task List_InvalidQuery_Returns400WithCamelCaseField(string query, string field)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.GetAsync(new Uri("/api/requests?" + query, UriKind.Relative), Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe([field]);
    }

    [Theory]
    [InlineData("lina", "P1")]       // R1
    [InlineData("karim", "P1")]      // R2
    [InlineData("fiona", "P2")]      // finance at finance stage
    [InlineData("pete", "P3")]       // procurement at fulfilment
    [InlineData("hana", "L1")]       // HR on leave
    public async Task Get_Visible_Returns200(string user, string request)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user);

        using var response = await client.GetAsync(RequestUri(org, request), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid().ShouldBe(org.Requests[request]);
    }

    [Theory]
    [InlineData("omar", "P1")]       // another employee's request
    [InlineData("boss", "P1")]       // second-level manager
    [InlineData("fiona", "L1")]      // finance can't see leave
    [InlineData("hana", "P2")]       // HR can't see purchases
    [InlineData("pete", "P2")]       // procurement can't see a purchase still at finance
    [InlineData("ada", "P1")]        // administrator
    [InlineData("gone", "P2")]       // inactive (reaches the endpoint within the 1-minute session window)
    public async Task Get_NotVisible_Returns404(string user, string request)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user, allowInactive: true);

        using var response = await client.GetAsync(RequestUri(org, request), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.GetAsync(new Uri($"/api/requests/{Guid.CreateVersion7()}", UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Theory]
    [InlineData("lina", true)]       // R1 requester
    [InlineData("karim", true)]      // R2 direct manager
    [InlineData("hana", true)]       // HrOfficer with R3
    public async Task Get_Leave_ReasonForRequesterManagerAndHr(string user, bool visible)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user);

        var leave = (await JsonBody.ReadAsync(await client.GetAsync(RequestUri(org, "L1"), Ct))).GetProperty("leave");

        leave.GetProperty("reasonHidden").GetBoolean().ShouldBe(!visible);
        if (visible)
        {
            leave.GetProperty("reason").GetString().ShouldBe("Family");
        }
    }

    [Fact]
    public async Task Get_Leave_FormerManagerSeesTheRequestButNotTheReason()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await SetManagerAsync(org.Users["lina"], org.Users["boss"]);
        using var karim = await ClientAsync(org, "karim");
        using var boss = await ClientAsync(org, "boss");

        var asFormer = (await JsonBody.ReadAsync(await karim.GetAsync(RequestUri(org, "L1"), Ct))).GetProperty("leave");
        var asCurrent = (await JsonBody.ReadAsync(await boss.GetAsync(RequestUri(org, "L1"), Ct))).GetProperty("leave");

        // karim decided the manager step (R3) but is no longer the direct manager (R2), and isn't HR.
        asFormer.GetProperty("reason").ValueKind.ShouldBe(JsonValueKind.Null);
        asFormer.GetProperty("reasonHidden").GetBoolean().ShouldBeTrue();
        asCurrent.GetProperty("reason").GetString().ShouldBe("Family");
    }

    [Fact]
    public async Task Get_Leave_FormerManagerWhoIsAlsoHrOfficer_StillDoesNotSeeTheReason()
    {
        // harry rejected nadia's leave at the manager step, so no HR task ever existed. HrOfficer + R3 only counts through
        // the HR task (AUTHORIZATION.md §5); as a former manager he sees the request but not the reason.
        var org = await AccessOrg.SeedAsync(Factory);
        var harry = await Given.User("harry").WithRoles(Roles.Employee, Roles.HrOfficer).CreateAsync(Factory);
        var nadia = await Given.User("nadia").ManagedBy(harry).CreateAsync(Factory);
        var hr = await Given.DepartmentAsync(Factory, "People", "PPL");
        var leaveId = await SeedLeaveRejectedByManagerAsync(nadia.Id, hr.Id, harry.Id);
        await SetManagerAsync(nadia.Id, org.Users["boss"]);
        using var client = await Factory.ClientFor(harry);

        var leave = (await JsonBody.ReadAsync(await client.GetAsync(new Uri($"/api/requests/{leaveId}", UriKind.Relative), Ct))).GetProperty("leave");

        leave.GetProperty("reason").ValueKind.ShouldBe(JsonValueKind.Null);
        leave.GetProperty("reasonHidden").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("lina")]
    [InlineData("karim")]
    [InlineData("hana")]
    public async Task ListAndActivity_NeverCarryTheLeaveReason(string user)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user);

        using var list = await client.GetAsync(new Uri("/api/requests?scope=visible", UriKind.Relative), Ct);
        using var activity = await client.GetAsync(RequestUri(org, "L1", "/activity"), Ct);

        activity.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync(Ct)).ShouldNotContain("Family");
        (await activity.Content.ReadAsStringAsync(Ct)).ShouldNotContain("Family");
    }

    [Theory]
    [InlineData("ada", false)]   // Administrator: no visibility
    [InlineData("gone", true)]   // inactive, session still alive
    [InlineData("boss", false)]  // second-level manager
    public async Task Activity_ForAdminInactiveOrSecondLevelManager_Returns404(string user, bool allowInactive)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user, allowInactive);

        using var response = await client.GetAsync(RequestUri(org, "L1", "/activity"), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=10001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_PagingOutOfRange_Returns400(string query)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.GetAsync(new Uri("/api/requests?" + query, UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task List_LastAllowedPage_ReturnsEmptyPage()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        var page = await ListAsync(lina, $"page={PageRequest.MaxPage}&pageSize={PageRequest.MaxPageSize}");

        page.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Activity_OverTheCap_KeepsTheNewestEntriesInChronologicalOrder()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await AddCommentsAsync(org.Requests["L1"], org.Users["lina"], RequestQueryService.ActivityCap + 5);
        using var lina = await ClientAsync(org, "lina");

        var items = (await JsonBody.ReadAsync(await lina.GetAsync(RequestUri(org, "L1", "/activity"), Ct))).EnumerateArray().ToList();

        items.Count.ShouldBe(RequestQueryService.ActivityCap);
        items[^1].GetProperty("body").GetString().ShouldBe($"c{RequestQueryService.ActivityCap + 4}");
        items.ShouldNotContain(i => i.GetProperty("type").GetString() == "Submitted"); // the oldest entries fell off
        items.Select(i => i.GetProperty("occurredAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public async Task Get_AvailableActionsAndTasks()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var karim = await ClientAsync(org, "karim");
        using var lina = await ClientAsync(org, "lina");
        using var pete = await ClientAsync(org, "pete");

        var asManager = await JsonBody.ReadAsync(await karim.GetAsync(RequestUri(org, "P1"), Ct));
        var asRequester = await JsonBody.ReadAsync(await lina.GetAsync(RequestUri(org, "P3"), Ct));
        var asProcurement = await JsonBody.ReadAsync(await pete.GetAsync(RequestUri(org, "P3"), Ct));

        var actions = asManager.GetProperty("availableActions");
        actions.GetProperty("actions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["approve", "reject"]);
        actions.GetProperty("taskId").GetGuid().ShouldBe(asManager.GetProperty("tasks")[0].GetProperty("id").GetGuid());
        actions.GetProperty("version").GetString().ShouldNotBeNullOrEmpty();
        asRequester.GetProperty("availableActions").ValueKind.ShouldBe(JsonValueKind.Null);
        asProcurement.GetProperty("availableActions").GetProperty("actions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["fulfill"]);

        var tasks = asRequester.GetProperty("tasks").EnumerateArray().ToList();
        tasks.Select(t => t.GetProperty("stage").GetString()).ShouldBe(["ManagerApproval", "FinanceApproval", "ProcurementFulfillment"]);
        tasks[1].GetProperty("assignee").GetProperty("role").GetString().ShouldBe("FinanceOfficer");
        tasks[1].GetProperty("decidedBy").GetProperty("displayName").GetString().ShouldBe("Fiona");
        tasks[1].GetProperty("outcome").GetString().ShouldBe("Approved");
        asRequester.GetProperty("status").GetString().ShouldBe("Approved");
        asRequester.GetProperty("canComment").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Activity_IsChronologicalWithActors()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.GetAsync(RequestUri(org, "P3", "/activity"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await JsonBody.ReadAsync(response)).EnumerateArray().ToList();
        items.Select(a => a.GetProperty("type").GetString())
            .ShouldBe(["Submitted", "TaskAssigned", "Approved", "TaskAssigned", "Approved", "TaskAssigned"]);
        items[0].GetProperty("actor").GetProperty("displayName").GetString().ShouldBe("Lina");
        items[1].GetProperty("actor").ValueKind.ShouldBe(JsonValueKind.Null);
        items[4].GetProperty("actor").GetProperty("displayName").GetString().ShouldBe("Fiona");
    }

    [Fact]
    public async Task Activity_NotVisible_Returns404()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var omar = await ClientAsync(org, "omar");

        using var response = await omar.GetAsync(RequestUri(org, "P1", "/activity"), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Comment_Returns201AndAppearsInActivity()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.PostAsJsonAsync(RequestUri(org, "P1", "/comments"), new { body = " Which model? " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/requests/{org.Requests["P1"]}/activity");
        var comment = await JsonBody.ReadAsync(response);
        comment.GetProperty("type").GetString().ShouldBe("CommentAdded");
        comment.GetProperty("body").GetString().ShouldBe("Which model?");
        comment.GetProperty("actor").GetProperty("id").GetGuid().ShouldBe(org.Users["lina"]);
        comment.GetProperty("stage").ValueKind.ShouldBe(JsonValueKind.Null);
        var activity = await JsonBody.ReadAsync(await lina.GetAsync(RequestUri(org, "P1", "/activity"), Ct));
        activity.EnumerateArray().Last().GetProperty("id").GetGuid().ShouldBe(comment.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Comment_NotifiesRequesterDecidersAndEligibleExceptTheAuthor()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var pete = await ClientAsync(org, "pete");

        (await pete.PostAsJsonAsync(RequestUri(org, "P3", "/comments"), new { body = "Ordered from vendor X" }, Ct)).Dispose();

        var notifications = await NotificationsAsync();
        // lina requester; karim and fiona decided; pete (only procurement officer) is the author.
        notifications.Select(n => n.RecipientId).Order().ShouldBe(new[] { org.Users["lina"], org.Users["karim"], org.Users["fiona"] }.Order());
        notifications.ShouldAllBe(n => n.Type == NotificationType.CommentAdded && n.RequestId == org.Requests["P3"]);
        notifications.ShouldAllBe(n => !(n.Title + n.Body).Contains("vendor X", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Comment_ByRequester_NotifiesTheEligibleRoleQueueButNotTheRequesterInIt()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var finny = await ClientAsync(org, "finny");

        (await finny.PostAsJsonAsync(RequestUri(org, "PF", "/comments"), new { body = "Any update?" }, Ct)).Dispose();

        // PF is at finance: fiona is eligible; finny is the author (and the requester); gone is inactive; karim decided.
        (await NotificationsAsync()).Select(n => n.RecipientId).Order()
            .ShouldBe(new[] { org.Users["karim"], org.Users["fiona"] }.Order());
    }

    [Fact]
    public async Task Comment_OnClosedRequest_IsAllowed()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p1 = await db.Requests.Include(r => r.Tasks).SingleAsync(r => r.Id == org.Requests["P1"], Ct);
            p1.DecideTask(p1.OpenTask!.Id, org.Users["karim"], TaskDecision.Reject("Over budget"),
                await Factory.PublishedWorkflowAsync(RequestType.EquipmentPurchase), _ => throw new InvalidOperationException(), Factory.Clock.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.PostAsJsonAsync(RequestUri(org, "P1", "/comments"), new { body = "Understood, thanks." }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("omar")]
    [InlineData("ada")]
    public async Task Comment_NotVisible_Returns404AndSavesNothing(string user)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var client = await ClientAsync(org, user);

        using var response = await client.PostAsJsonAsync(RequestUri(org, "P1", "/comments"), new { body = "Hi" }, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public async Task Comment_InvalidLength_Returns400(int length)
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.PostAsJsonAsync(RequestUri(org, "P1", "/comments"), new { body = new string('c', length) }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["body"]);
    }

    [Fact]
    public async Task Comment_WhitespaceOnly_Returns400()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        using var lina = await ClientAsync(org, "lina");

        using var response = await lina.PostAsJsonAsync(RequestUri(org, "P1", "/comments"), new { body = "   " }, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    private async Task<HttpClient> ClientAsync(AccessOrg org, string user, bool allowInactive = false)
    {
        if (!allowInactive || user != "gone")
        {
            return await Factory.ClientFor(new TestUser(org.Users[user], $"{user}@flowops.test", Given.DefaultPassword, user));
        }

        // Inactive users can't sign in: sign in while active, then deactivate (the session lives until re-validation).
        await SetActiveAsync(org.Users[user], true);
        var client = await Factory.ClientFor(new TestUser(org.Users[user], $"{user}@flowops.test", Given.DefaultPassword, user));
        await SetActiveAsync(org.Users[user], false);
        return client;
    }

    private static Uri RequestUri(AccessOrg org, string request, string suffix = "") =>
        new($"/api/requests/{org.Requests[request]}{suffix}", UriKind.Relative);

    private static async Task<JsonElement> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri("/api/requests?" + query, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await JsonBody.ReadAsync(response);
    }

    private static List<string> Names(AccessOrg org, JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => org.RequestName(i.GetProperty("id").GetGuid()))];

    /// <summary>AccessOrg submits everything at one instant; give P1…PF increasing times so "newest first" is observable.</summary>
    private async Task SpreadSubmissionTimesAsync(AccessOrg org)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var minute = 0;
        foreach (var name in new[] { "P1", "P2", "P3", "L1", "PF" })
        {
            var at = Factory.Clock.GetUtcNow().AddMinutes(minute++);
            await db.Requests.Where(r => r.Id == org.Requests[name]).ExecuteUpdateAsync(s => s.SetProperty(r => r.SubmittedAt, at), Ct);
        }
    }

    private async Task<Guid> SeedLeaveRejectedByManagerAsync(Guid requesterId, Guid departmentId, Guid managerId)
    {
        var now = Factory.Clock.GetUtcNow();
        var workflow = await Factory.PublishedWorkflowAsync(RequestType.Leave);
        Assignee Resolve(WorkflowStep step) =>
            step.Assignee.IsRequesterManager ? Assignee.ForUser(managerId) : Assignee.ForRole(step.Assignee.Role!);
        var leave = Request.SubmitLeave(RequestNumber.Create(2026, 90), requesterId, departmentId,
            LeaveDetails.Create(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12), LeaveType.Sick, "Surgery", now, TimeZoneInfo.Utc),
            workflow, Resolve, now);
        leave.DecideTask(leave.OpenTask!.Id, managerId, TaskDecision.Reject("Too busy"), workflow, Resolve, now);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Requests.Add(leave);
        await db.SaveChangesAsync(Ct);
        return leave.Id;
    }

    private async Task AddCommentsAsync(Guid requestId, Guid authorId, int count)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.Requests.SingleAsync(r => r.Id == requestId, Ct);
        var start = Factory.Clock.GetUtcNow();
        for (var i = 0; i < count; i++)
        {
            request.AddComment(authorId, $"c{i}", start.AddSeconds(i + 1));
        }

        await db.SaveChangesAsync(Ct);
    }

    private async Task SetManagerAsync(Guid userId, Guid managerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, managerId), Ct);
    }

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive), Ct);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AsNoTracking().ToListAsync(Ct);
    }
}
