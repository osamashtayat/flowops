using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;

namespace FlowOps.Api.IntegrationTests.Notifications;

/// <summary>
/// BE-13 (API_CONTRACT.md §6, TESTING.md §3.5): users read and mark only their own notifications. Notifications are
/// arranged the way they arise: employees submit requests, which notify their manager.
/// <code>karim manages lina and omar · mona manages nour</code>
/// </summary>
public sealed class NotificationsApiTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string LeaveReason = "Hospital appointment";
    private static readonly Uri List = new("/api/notifications", UriKind.Relative);
    private static readonly Uri UnreadCount = new("/api/notifications/unread-count", UriKind.Relative);
    private static readonly Uri ReadAll = new("/api/notifications/read-all", UriKind.Relative);

    private TestUser _karim = null!;
    private TestUser _lina = null!;
    private TestUser _omar = null!;
    private TestUser _mona = null!;
    private TestUser _nour = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        _karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        _mona = await Given.User("mona").InDepartment(sales).CreateAsync(Factory);
        _lina = await Given.User("lina").InDepartment(sales).ManagedBy(_karim).CreateAsync(Factory);
        _omar = await Given.User("omar").InDepartment(sales).ManagedBy(_karim).CreateAsync(Factory);
        _nour = await Given.User("nour").InDepartment(sales).ManagedBy(_mona).CreateAsync(Factory);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnNotificationsNewestFirst()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await SubmitPurchaseAsync(_omar, "Monitor");
        await SubmitPurchaseAsync(_nour, "Chair");
        using var karim = await Factory.ClientFor(_karim);

        var page = await JsonBody.ReadAsync(await karim.GetAsync(List, Ct));

        page.GetProperty("totalCount").GetInt32().ShouldBe(2);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("body").GetString()).ShouldBe(
            [$"Omar submitted a purchase request: Monitor.", $"Lina submitted a purchase request: Laptop."]);
        items[0].GetProperty("type").GetString().ShouldBe("TaskAssigned");
        items[0].GetProperty("requestId").GetString().ShouldNotBeNull();
        items[0].GetProperty("taskId").GetString().ShouldNotBeNull();
        items[0].GetProperty("readAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_UnreadOnlyAndUnreadCount_IgnoreReadNotifications()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        await SubmitPurchaseAsync(_omar, "Monitor");
        using var karim = await Factory.ClientFor(_karim);
        var first = await FirstIdAsync(karim);
        (await karim.PostAsync(ReadUri(first), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var unread = await JsonBody.ReadAsync(await karim.GetAsync(new Uri(List + "?unreadOnly=true", UriKind.Relative), Ct));
        var count = await JsonBody.ReadAsync(await karim.GetAsync(UnreadCount, Ct));

        unread.GetProperty("totalCount").GetInt32().ShouldBe(1);
        unread.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldNotBe(first);
        count.GetProperty("count").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_Pages()
    {
        for (var i = 0; i < 3; i++)
        {
            await SubmitPurchaseAsync(_lina, $"Item {i}");
            Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        using var karim = await Factory.ClientFor(_karim);

        var page = await JsonBody.ReadAsync(await karim.GetAsync(new Uri(List + "?page=2&pageSize=2", UriKind.Relative), Ct));

        page.GetProperty("totalCount").GetInt32().ShouldBe(3);
        page.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("body").GetString().ShouldBe("Lina submitted a purchase request: Item 0.");
    }

    [Fact]
    public async Task MarkRead_IsIdempotentAndKeepsTheFirstReadTime()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        using var karim = await Factory.ClientFor(_karim);
        var id = await FirstIdAsync(karim);
        (await karim.PostAsync(ReadUri(id), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        using var again = await karim.PostAsync(ReadUri(id), null, Ct);

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var item = (await JsonBody.ReadAsync(await karim.GetAsync(List, Ct))).GetProperty("items")[0];
        item.GetProperty("readAt").GetDateTimeOffset().ShouldBe(TestClock.Start);
    }

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_Returns404AndLeavesItUnread()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        using var karim = await Factory.ClientFor(_karim);
        var karimsNotification = await FirstIdAsync(karim);
        using var mona = await Factory.ClientFor(_mona);

        using var response = await mona.PostAsync(ReadUri(karimsNotification), null, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
        (await JsonBody.ReadAsync(await karim.GetAsync(UnreadCount, Ct))).GetProperty("count").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task MarkRead_UnknownId_Returns404()
    {
        using var karim = await Factory.ClientFor(_karim);

        using var response = await karim.PostAsync(ReadUri(Guid.CreateVersion7()), null, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ReadAll_MarksOnlyTheCallersNotifications()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        await SubmitPurchaseAsync(_omar, "Monitor");
        await SubmitPurchaseAsync(_nour, "Chair");
        using var karim = await Factory.ClientFor(_karim);
        using var mona = await Factory.ClientFor(_mona);

        using var response = await karim.PostAsync(ReadAll, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await JsonBody.ReadAsync(await karim.GetAsync(UnreadCount, Ct))).GetProperty("count").GetInt32().ShouldBe(0);
        (await JsonBody.ReadAsync(await mona.GetAsync(UnreadCount, Ct))).GetProperty("count").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ReadAll_WithNothingUnread_Returns204()
    {
        using var karim = await Factory.ClientFor(_karim);

        using var response = await karim.PostAsync(ReadAll, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task List_NeverContainsTheLeaveReason()
    {
        using (var lina = await Factory.ClientFor(_lina))
        {
            (await lina.PostAsJsonAsync(new Uri("/api/requests/leave", UriKind.Relative),
                new { startDate = "2026-03-10", endDate = "2026-03-12", leaveType = "Annual", reason = LeaveReason }, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var karim = await Factory.ClientFor(_karim);

        var raw = await (await karim.GetAsync(List, Ct)).Content.ReadAsStringAsync(Ct);

        raw.ShouldContain("TaskAssigned");
        raw.ShouldNotContain(LeaveReason);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?unreadOnly=maybe")]
    public async Task List_InvalidQuery_Returns400(string query)
    {
        using var karim = await Factory.ClientFor(_karim);

        using var response = await karim.GetAsync(new Uri(List + query, UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task Endpoints_WithoutASession_Return401()
    {
        using var anonymous = Factory.CreateHttpsClient();

        using var list = await anonymous.GetAsync(List, Ct);
        using var count = await anonymous.GetAsync(UnreadCount, Ct);

        await ProblemAssert.HasCode(list, 401, ErrorCodes.AuthUnauthenticated);
        await ProblemAssert.HasCode(count, 401, ErrorCodes.AuthUnauthenticated);
    }

    [Fact]
    public async Task Admin_WithoutProcessRoles_SeesOnlyTheirOwnNotifications()
    {
        await SubmitPurchaseAsync(_lina, "Laptop");
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        var page = await JsonBody.ReadAsync(await admin.GetAsync(List, Ct));

        page.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    private async Task SubmitPurchaseAsync(TestUser requester, string item)
    {
        using var client = await Factory.ClientFor(requester);
        using var response = await client.PostAsJsonAsync(new Uri("/api/requests/purchase", UriKind.Relative), new
        {
            itemName = item, description = "For work", estimatedCost = new { amount = "100.000", currency = "JOD" }, justification = "Needed",
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<Guid> FirstIdAsync(HttpClient client) =>
        (await JsonBody.ReadAsync(await client.GetAsync(List, Ct))).GetProperty("items")[0].GetProperty("id").GetGuid();

    private static Uri ReadUri(Guid id) => new($"/api/notifications/{id}/read", UriKind.Relative);
}
