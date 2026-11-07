using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowOps.Api.IntegrationTests.Realtime;

/// <summary>
/// BE-14 (API_CONTRACT.md §9, ADR-0009, TESTING.md §3.5): hints are published after the commit, to the right people, never
/// when the change didn't commit, and a publishing failure never fails the request. One test goes through the real hub.
/// <code>karim manages lina · fiona, finny: FinanceOfficer (finny also reports to karim) · omar: unrelated employee</code>
/// </summary>
public sealed class RealtimeTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly string[] AdminAndEmployee = ["Administrator", "Employee"];

    private TestUser _karim = null!;
    private TestUser _lina = null!;
    private TestUser _fiona = null!;
    private TestUser _finny = null!;
    private TestUser _omar = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        _karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        _lina = await Given.User("lina").InDepartment(sales).ManagedBy(_karim).CreateAsync(Factory);
        _finny = await Given.User("finny").InDepartment(sales).ManagedBy(_karim).WithRoles(Roles.Employee, Roles.FinanceOfficer).CreateAsync(Factory);
        _fiona = await Given.User("fiona").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);
        _omar = await Given.User("omar").InDepartment(sales).CreateAsync(Factory);
    }

    [Fact]
    public async Task Submit_PublishesTheManagersNotificationTheRequestAndTheirInbox()
    {
        var requestId = await SubmitPurchaseAsync(_lina);

        var notification = await SingleNotificationAsync(_karim.Id);
        Factory.Realtime.Events.Select(Key).ShouldBe(
        [
            Key(new RealtimeEvent("notificationCreated", [_karim.Id], notification, requestId)),
            Key(Event("requestChanged", requestId, _lina, _karim)),
            Key(Event("tasksChanged", null, _karim)),
        ]);
    }

    [Fact]
    public async Task Decision_PublishesToTheNextQueueTheRequesterTheViewersAndTheDecider()
    {
        var requestId = await SubmitPurchaseAsync(_lina);
        Factory.Realtime.Reset();

        await ApproveOpenTaskAsync(_karim, requestId);

        var events = Factory.Realtime.Events;
        events.Where(e => e.Name == "notificationCreated").SelectMany(e => e.Recipients).Order()
            .ShouldBe(new[] { _lina.Id, _fiona.Id, _finny.Id }.Order());
        events.Single(e => e.Name == "requestChanged").Recipients.Order()
            .ShouldBe(new[] { _lina.Id, _karim.Id, _fiona.Id, _finny.Id }.Order());
        events.Single(e => e.Name == "tasksChanged").Recipients.Order()
            .ShouldBe(new[] { _karim.Id, _fiona.Id, _finny.Id }.Order());
        events.SelectMany(e => e.Recipients).ShouldNotContain(_omar.Id);
    }

    [Fact]
    public async Task Comment_PublishesTheNotificationAndRequestChanged()
    {
        var requestId = await SubmitPurchaseAsync(_lina);
        Factory.Realtime.Reset();
        using var karim = await Factory.ClientFor(_karim);

        (await karim.PostAsJsonAsync(new Uri($"/api/requests/{requestId}/comments", UriKind.Relative), new { body = "Which model?" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var events = Factory.Realtime.Events;
        events.Single(e => e.Name == "notificationCreated").Recipients.ShouldBe([_lina.Id]);
        events.Single(e => e.Name == "requestChanged").Recipients.Order().ShouldBe(new[] { _lina.Id, _karim.Id }.Order());
        events.ShouldNotContain(e => e.Name == "tasksChanged");
    }

    [Fact]
    public async Task OwnRoleChange_PublishesTheOtherAdminsNotification()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var client = await Factory.ClientFor(ada);

        (await client.PutAsJsonAsync(new Uri($"/api/users/{ada.Id}/roles", UriKind.Relative), new { roles = AdminAndEmployee }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var created = Factory.Realtime.Events.ShouldHaveSingleItem();
        created.Name.ShouldBe("notificationCreated");
        created.Recipients.ShouldBe([bea.Id]);
        created.RequestId.ShouldBeNull();
    }

    [Fact]
    public async Task FailedSave_WritesNoNotificationsAndPublishesNothing()
    {
        var requestId = await SubmitPurchaseAsync(_lina);
        Factory.Realtime.Reset();
        var (taskId, version) = await OpenTaskAsync(_karim, requestId);
        using var karim = await Factory.ClientFor(_karim);
        Factory.SaveFailures.FailNextSave();

        using var response = await karim.PostAsJsonAsync(new Uri($"/api/tasks/{taskId}/approve", UriKind.Relative), new { version }, Ct);

        await ProblemAssert.HasCode(response, 500, ErrorCodes.ServerError);
        Factory.Realtime.Events.ShouldBeEmpty();
        (await NotificationCountAsync()).ShouldBe(1); // only the manager's from the submission
    }

    [Fact]
    public async Task RejectedDecision_PublishesNothing()
    {
        var requestId = await SubmitPurchaseAsync(_lina);
        Factory.Realtime.Reset();
        var (taskId, _) = await OpenTaskAsync(_karim, requestId);
        using var karim = await Factory.ClientFor(_karim);

        using var response = await karim.PostAsJsonAsync(new Uri($"/api/tasks/{taskId}/approve", UriKind.Relative), new { version = "1" }, Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.ConcurrencyConflict);
        Factory.Realtime.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublisherFailure_IsLoggedAndTheDecisionStillSucceeds()
    {
        var requestId = await SubmitPurchaseAsync(_lina);
        Factory.Realtime.FailPublishing = true;

        await ApproveOpenTaskAsync(_karim, requestId);

        (await NotificationCountAsync()).ShouldBe(4); // karim's from the submission, then fiona, finny and lina
        Factory.Logs.Entries.Count(e => e.Level == LogLevel.Warning && e.Mentions("could not be published")).ShouldBe(3);
    }

    [Fact]
    public async Task Hub_WithACookieSession_ReceivesOnlyTheCallersEvents()
    {
        await using var karimHub = await ConnectAsync(_karim);
        await using var omarHub = await ConnectAsync(_omar);
        var karimEvents = Listen(karimHub.Connection);
        var omarEvents = Listen(omarHub.Connection);

        var requestId = await SubmitPurchaseAsync(_lina);

        var notification = await karimEvents.ReadAsync(Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);
        notification.GetProperty("notificationId").GetGuid().ShouldBe(await SingleNotificationAsync(_karim.Id));
        notification.GetProperty("requestId").GetGuid().ShouldBe(requestId);
        omarEvents.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Hub_WithoutASession_Returns401()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await client.PostAsync(new Uri("/hubs/notifications/negotiate?negotiateVersion=1", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private static RealtimeEvent Event(string name, Guid? requestId, params TestUser[] recipients) =>
        new(name, [.. recipients.Select(r => r.Id)], null, requestId);

    private async Task<Guid> SubmitPurchaseAsync(TestUser requester)
    {
        using var client = await Factory.ClientFor(requester);
        using var response = await client.PostAsJsonAsync(new Uri("/api/requests/purchase", UriKind.Relative), new
        {
            itemName = "Laptop", description = "14 inch", estimatedCost = new { amount = "1250.500", currency = "JOD" }, justification = "Broken",
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid TaskId, string Version)> OpenTaskAsync(TestUser user, Guid requestId)
    {
        using var client = await Factory.ClientFor(user);
        var details = await JsonBody.ReadAsync(await client.GetAsync(new Uri($"/api/requests/{requestId}", UriKind.Relative), Ct));
        var available = details.GetProperty("availableActions");
        return (available.GetProperty("taskId").GetGuid(), available.GetProperty("version").GetString()!);
    }

    private async Task ApproveOpenTaskAsync(TestUser user, Guid requestId)
    {
        var (taskId, version) = await OpenTaskAsync(user, requestId);
        using var client = await Factory.ClientFor(user);
        (await client.PostAsJsonAsync(new Uri($"/api/tasks/{taskId}/approve", UriKind.Relative), new { version }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<Guid> SingleNotificationAsync(Guid recipientId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .Where(n => n.RecipientId == recipientId).Select(n => n.Id).SingleAsync(Ct);
    }

    private async Task<int> NotificationCountAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.CountAsync(Ct);
    }

    /// <summary>A real hub connection over long polling, authenticated by the session cookie from a real login.</summary>
    private async Task<HubSession> ConnectAsync(TestUser user)
    {
        using var client = Factory.CreateHttpsClient();
        using var login = await FlowOpsApiFactory.LoginAsync(client, user.Email, user.Password);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-flowops=", StringComparison.Ordinal)).Split(';')[0];
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .Build();
        await connection.StartAsync(Ct);
        return new HubSession(connection);
    }

    private static ChannelReader<JsonElement> Listen(HubConnection connection)
    {
        var channel = Channel.CreateUnbounded<JsonElement>();
        connection.On<JsonElement>("notificationCreated", payload => channel.Writer.TryWrite(payload));
        return channel.Reader;
    }

    private sealed class HubSession(HubConnection connection) : IAsyncDisposable
    {
        public HubConnection Connection { get; } = connection;

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    /// <summary>An event as a comparable string, recipients as a sorted set.</summary>
    private static string Key(RealtimeEvent e) =>
        $"{e.Name}|{string.Join(",", e.Recipients.Order())}|{e.NotificationId}|{e.RequestId}";
}
