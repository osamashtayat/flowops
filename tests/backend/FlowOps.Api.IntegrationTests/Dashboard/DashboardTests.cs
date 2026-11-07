using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;

namespace FlowOps.Api.IntegrationTests.Dashboard;

/// <summary>
/// BE-15: <c>GET /api/dashboard</c> (API_CONTRACT.md §7) and <c>GET /api/admin/stalled-tasks</c> (§8), arranged through
/// the API. <code>karim manages lina, omar, finny · fiona, finny: FinanceOfficer · ada: Administrator</code>
/// </summary>
public sealed class DashboardTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly Uri Dashboard = new("/api/dashboard", UriKind.Relative);
    private static readonly Uri Stalled = new("/api/admin/stalled-tasks", UriKind.Relative);

    private TestUser _karim = null!;
    private TestUser _lina = null!;
    private TestUser _omar = null!;
    private TestUser _finny = null!;
    private TestUser _fiona = null!;
    private TestUser _ada = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        await Given.DepartmentAsync(Factory, "Finance", "FIN");
        _karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        _lina = await Given.User("lina").InDepartment(sales).ManagedBy(_karim).CreateAsync(Factory);
        _omar = await Given.User("omar").InDepartment(sales).ManagedBy(_karim).CreateAsync(Factory);
        _finny = await Given.User("finny").InDepartment(sales).ManagedBy(_karim).WithRoles(Roles.Employee, Roles.FinanceOfficer).CreateAsync(Factory);
        _fiona = await Given.User("fiona").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);
        _ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
    }

    // ---- Dashboard ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Employee_SeesOwnRequestCountsAndRecentButNoTasksOrAdminSection()
    {
        var inReview = await SubmitPurchaseAsync(_lina, "Laptop");
        var rejected = await SubmitPurchaseAsync(_lina, "Monitor");
        var approved = await SubmitPurchaseAsync(_lina, "Chair");
        await DecideAsync(_karim, rejected, "reject");
        await DecideAsync(_karim, approved, "approve");
        await DecideAsync(_fiona, approved, "approve");

        var dashboard = await DashboardAsync(_lina);

        var mine = dashboard.GetProperty("myRequests");
        (mine.GetProperty("inReview").GetInt32(), mine.GetProperty("approved").GetInt32(), mine.GetProperty("completed").GetInt32(),
            mine.GetProperty("rejected").GetInt32()).ShouldBe((1, 1, 0, 1));
        Ids(mine.GetProperty("recent")).ShouldBe([approved, rejected, inReview]);
        dashboard.GetProperty("myTasks").ValueKind.ShouldBe(JsonValueKind.Null);
        dashboard.GetProperty("admin").ValueKind.ShouldBe(JsonValueKind.Null);
        var activity = dashboard.GetProperty("recentActivity").EnumerateArray().ToList();
        activity.Count.ShouldBe(10);
        activity[0].GetProperty("requestId").GetGuid().ShouldBe(approved);
        activity[0].GetProperty("type").GetString().ShouldBe("TaskAssigned");
        activity[0].GetProperty("requestNumber").GetString().ShouldStartWith("FO-");
        activity.Select(a => a.GetProperty("occurredAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Employee_RecentHoldsAtMostFiveNewestFirst()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            ids.Add(await SubmitPurchaseAsync(_lina, $"Item {i}"));
            Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        var recent = (await DashboardAsync(_lina)).GetProperty("myRequests").GetProperty("recent");

        Ids(recent).ShouldBe([.. Enumerable.Reverse(ids).Take(5)]);
    }

    [Fact]
    public async Task Manager_SeesOpenTasksOldestFirstAndRecentDecisions()
    {
        var first = await SubmitPurchaseAsync(_lina, "Laptop");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await SubmitPurchaseAsync(_omar, "Monitor");
        var decided = await SubmitPurchaseAsync(_omar, "Chair");
        await DecideAsync(_karim, decided, "approve");

        var tasks = (await DashboardAsync(_karim)).GetProperty("myTasks");

        tasks.GetProperty("open").GetInt32().ShouldBe(2);
        tasks.GetProperty("oldestOpenCreatedAt").GetDateTimeOffset().ShouldBe(TestClock.Start);
        tasks.GetProperty("decidedLast7Days").GetInt32().ShouldBe(1);
        first.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Manager_DecisionsOlderThanSevenDaysDropOut()
    {
        var request = await SubmitPurchaseAsync(_lina, "Laptop");
        await DecideAsync(_karim, request, "approve");
        Factory.Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        var tasks = (await DashboardAsync(_karim)).GetProperty("myTasks");

        tasks.GetProperty("decidedLast7Days").GetInt32().ShouldBe(0);
        tasks.GetProperty("open").GetInt32().ShouldBe(0);
        tasks.GetProperty("oldestOpenCreatedAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Officer_WithoutOwnRequests_GetsZerosAndTheirQueue()
    {
        var request = await SubmitPurchaseAsync(_lina, "Laptop");
        await DecideAsync(_karim, request, "approve");

        var dashboard = await DashboardAsync(_fiona);

        var mine = dashboard.GetProperty("myRequests");
        mine.GetProperty("inReview").GetInt32().ShouldBe(0);
        mine.GetProperty("recent").GetArrayLength().ShouldBe(0);
        dashboard.GetProperty("myTasks").GetProperty("open").GetInt32().ShouldBe(1);
        dashboard.GetProperty("admin").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Admin_GetsTheAdminSectionAndNoRequestData()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        await Given.User("gone").WithRoles(Roles.FinanceOfficer).Inactive().CreateAsync(Factory);
        await DeactivateAsync(_karim); // the open manager task is now stalled

        var dashboard = await DashboardAsync(_ada);

        var admin = dashboard.GetProperty("admin");
        admin.GetProperty("activeUsers").GetInt32().ShouldBe(5); // lina, omar, finny, fiona, ada
        admin.GetProperty("departments").GetInt32().ShouldBe(2);
        admin.GetProperty("stalledTasks").GetInt32().ShouldBe(1);
        dashboard.GetProperty("myTasks").ValueKind.ShouldBe(JsonValueKind.Null);
        dashboard.GetProperty("recentActivity").GetArrayLength().ShouldBe(0);
        dashboard.GetProperty("myRequests").GetProperty("recent").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task RecentActivity_OnlyCoversVisibleRequests()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");

        (await DashboardAsync(_omar)).GetProperty("recentActivity").GetArrayLength().ShouldBe(0);
        (await DashboardAsync(_fiona)).GetProperty("recentActivity").GetArrayLength().ShouldBe(0);
        (await DashboardAsync(_karim)).GetProperty("recentActivity").GetArrayLength().ShouldBe(2);
    }

    [Theory]
    [InlineData("karim")]
    [InlineData("ada")]
    [InlineData("finny")]
    public async Task Dashboard_TakesAtMostFourSqlRoundTrips(string user)
    {
        var request = await SubmitPurchaseAsync(_lina, "Laptop");
        await DecideAsync(_karim, request, "approve");
        using var client = await Factory.ClientFor(User(user));

        Factory.Sql.Start();
        using var response = await client.GetAsync(Dashboard, Ct);
        var commands = Factory.Sql.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        commands.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task Dashboard_WithoutASession_Returns401()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await client.GetAsync(Dashboard, Ct);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    // ---- Stalled tasks -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Stalled_ListsAnInactiveManagerAndARoleQueueWithOnlyTheRequester()
    {
        var finnysOwn = await SubmitPurchaseAsync(_finny, "Laptop");
        await DecideAsync(_karim, finnysOwn, "approve"); // finance: fiona and finny; finny is the requester
        await DeactivateAsync(_fiona);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var linas = await SubmitPurchaseAsync(_lina, "Monitor");
        await DeactivateAsync(_karim);

        var page = await StalledAsync(_ada, "");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("requestId").GetGuid()).ShouldBe([finnysOwn, linas]);
        items[0].GetProperty("reason").GetString().ShouldBe("NoEligibleRoleMember");
        items[0].GetProperty("stage").GetString().ShouldBe("FinanceApproval");
        items[0].GetProperty("assignee").GetProperty("role").GetString().ShouldBe("FinanceOfficer");
        items[0].GetProperty("assignee").GetProperty("user").ValueKind.ShouldBe(JsonValueKind.Null);
        items[1].GetProperty("reason").GetString().ShouldBe("AssigneeInactive");
        items[1].GetProperty("assignee").GetProperty("user").GetProperty("id").GetGuid().ShouldBe(_karim.Id);
        items[1].GetProperty("requestType").GetString().ShouldBe("EquipmentPurchase");
        items[1].GetProperty("requestNumber").GetString().ShouldStartWith("FO-");
        items[1].GetProperty("openSince").GetDateTimeOffset().ShouldBe(TestClock.Start.AddMinutes(1));
        items[1].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ShouldBe(
            ["assignee", "openSince", "reason", "requestId", "requestNumber", "requestType", "stage", "taskId"]);
    }

    [Fact]
    public async Task Stalled_RoleQueueUnblocksWhenAnEligibleOfficerIsAdded()
    {
        var finnysOwn = await SubmitPurchaseAsync(_finny, "Laptop");
        await DecideAsync(_karim, finnysOwn, "approve");
        await DeactivateAsync(_fiona);
        (await StalledAsync(_ada, "")).GetProperty("totalCount").GetInt32().ShouldBe(1);

        var fred = await Given.User("fred").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);

        (await StalledAsync(_ada, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        await DecideAsync(fred, finnysOwn, "approve");
    }

    [Fact]
    public async Task Stalled_IsEmptyWhenEveryOpenTaskHasADecider()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");

        var page = await StalledAsync(_ada, "");

        page.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Stalled_Pages()
    {
        for (var i = 0; i < 3; i++)
        {
            await SubmitPurchaseAsync(_lina, $"Item {i}");
        }

        await DeactivateAsync(_karim);

        var page = await StalledAsync(_ada, "?page=2&pageSize=2");

        page.GetProperty("totalCount").GetInt32().ShouldBe(3);
        page.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData("karim")]
    [InlineData("fiona")]
    public async Task Stalled_ForNonAdmins_Returns403(string user)
    {
        using var client = await Factory.ClientFor(User(user));

        using var response = await client.GetAsync(Stalled, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task Stalled_InvalidPaging_Returns400()
    {
        using var client = await Factory.ClientFor(_ada);

        using var response = await client.GetAsync(new Uri(Stalled + "?pageSize=101", UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------------

    private TestUser User(string name) => name switch
    {
        "karim" => _karim,
        "lina" => _lina,
        "omar" => _omar,
        "finny" => _finny,
        "fiona" => _fiona,
        "ada" => _ada,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static Guid[] Ids(JsonElement array) => [.. array.EnumerateArray().Select(r => r.GetProperty("id").GetGuid())];

    private async Task<JsonElement> DashboardAsync(TestUser user)
    {
        using var client = await Factory.ClientFor(user);
        using var response = await client.GetAsync(Dashboard, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonBody.ReadAsync(response);
    }

    private async Task<JsonElement> StalledAsync(TestUser user, string query)
    {
        using var client = await Factory.ClientFor(user);
        using var response = await client.GetAsync(new Uri(Stalled + query, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonBody.ReadAsync(response);
    }

    private async Task<Guid> SubmitPurchaseAsync(TestUser requester, string item)
    {
        using var client = await Factory.ClientFor(requester);
        using var response = await client.PostAsJsonAsync(new Uri("/api/requests/purchase", UriKind.Relative), new
        {
            itemName = item, description = "For work", estimatedCost = new { amount = "100.000", currency = "JOD" }, justification = "Needed",
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task DecideAsync(TestUser decider, Guid requestId, string action)
    {
        using var client = await Factory.ClientFor(decider);
        var details = await JsonBody.ReadAsync(await client.GetAsync(new Uri($"/api/requests/{requestId}", UriKind.Relative), Ct));
        var available = details.GetProperty("availableActions");
        object body = action == "reject"
            ? new { version = available.GetProperty("version").GetString(), reason = "No" }
            : new { version = available.GetProperty("version").GetString() };
        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/tasks/{available.GetProperty("taskId").GetGuid()}/{action}", UriKind.Relative), body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task DeactivateAsync(TestUser user)
    {
        using var admin = await Factory.ClientFor(_ada);
        using var response = await admin.PutAsJsonAsync(new Uri($"/api/users/{user.Id}", UriKind.Relative),
            new { displayName = user.DisplayName, departmentId = (Guid?)null, managerId = (Guid?)null, isActive = false }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }
}
