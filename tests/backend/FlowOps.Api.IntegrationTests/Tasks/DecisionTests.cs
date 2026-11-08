using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Tasks;

/// <summary>
/// BE-12: the inbox and the three decisions through HTTP (WORKFLOWS.md §2–5, TESTING.md §3.2–3.4). Everything is
/// arranged through the API except the org itself.
/// <code>
/// karim manages lina, finny, omar · fiona, finny: FinanceOfficer · hana: HrOfficer · pete: ProcurementOfficer
/// ada: Administrator · gone: inactive FinanceOfficer
/// </code>
/// </summary>
public sealed class DecisionTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string LeaveReason = "Hospital appointment";
    private static readonly Uri Purchase = new("/api/requests/purchase", UriKind.Relative);
    private static readonly Uri Leave = new("/api/requests/leave", UriKind.Relative);

    private Dictionary<string, TestUser> _users = [];

    // ---- Happy paths (§3.2) ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Purchase_EndToEnd_MovesThroughEveryStepAndNotifiesTheRightPeople()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");

        var managerItem = await OpenItemAsync("karim", requestId);
        managerItem.GetProperty("stage").GetString().ShouldBe("ManagerApproval");
        Strings(managerItem, "actions").ShouldBe(["approve", "reject"]);
        managerItem.GetProperty("request").GetProperty("keyDetail").GetString().ShouldBe("JOD 1,250.500");
        (await DecideAsync("karim", managerItem, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeAtAsync(requestId, "InReview", "FinanceApproval");
        (await OpenItemAsync("finny", requestId)).ValueKind.ShouldBe(JsonValueKind.Object);

        (await DecideAsync("fiona", await OpenItemAsync("fiona", requestId), "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeAtAsync(requestId, "Approved", "ProcurementFulfillment");

        var fulfillment = await OpenItemAsync("pete", requestId);
        Strings(fulfillment, "actions").ShouldBe(["fulfill"]);
        (await DecideAsync("pete", fulfillment, "fulfill", note: "PO-77")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeAtAsync(requestId, "Completed", "Closed");

        (await NotificationsAsync(requestId)).Select(n => (Name(n.RecipientId), n.Type)).Order().ShouldBe(new[]
        {
            ("fiona", NotificationType.TaskAssigned),
            ("finny", NotificationType.TaskAssigned),
            ("karim", NotificationType.TaskAssigned),
            ("lina", NotificationType.RequestApproved),
            ("lina", NotificationType.RequestCompleted),
            ("lina", NotificationType.StepApproved),
            ("pete", NotificationType.TaskAssigned),
        }.Order());
        (await ActivityTypesAsync(requestId)).ShouldBe(
            ["Submitted", "TaskAssigned", "Approved", "TaskAssigned", "Approved", "TaskAssigned", "Fulfilled", "Completed"]);
    }

    [Fact]
    public async Task Leave_EndToEnd_CompletesAfterHrAndNeverPutsTheReasonInNotifications()
    {
        await SeedOrgAsync();
        var requestId = await SubmitLeaveAsync("lina");

        var managerItem = await OpenItemAsync("karim", requestId);
        managerItem.GetProperty("request").GetProperty("keyDetail").GetString().ShouldBe("3 days · Annual");
        (await DecideAsync("karim", managerItem, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeAtAsync(requestId, "InReview", "HrConfirmation");
        (await OpenItemsAsync("fiona")).ShouldBeEmpty();

        (await DecideAsync("hana", await OpenItemAsync("hana", requestId), "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldBeAtAsync(requestId, "Completed", "Closed");
        var notifications = await NotificationsAsync(requestId);
        notifications.Where(n => Name(n.RecipientId) == "lina").Select(n => n.Type).Order()
            .ShouldBe(new[] { NotificationType.RequestCompleted, NotificationType.StepApproved }.Order());
        notifications.ShouldAllBe(n => !n.Title.Contains(LeaveReason) && !n.Body.Contains(LeaveReason));
    }

    [Fact]
    public async Task Reject_AtFinance_ClosesTheRequestAndTellsTheRequesterWhy()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");

        using var response = await DecideAsync("fiona", await OpenItemAsync("fiona", requestId), "reject", reason: "Over budget");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeAtAsync(requestId, "Rejected", "Closed");
        (await OpenItemsAsync("pete")).ShouldBeEmpty();
        (await NotificationsAsync(requestId)).Single(n => n.Type == NotificationType.RequestRejected).Body.ShouldContain("Over budget");
        (await ActivityTypesAsync(requestId))[^1].ShouldBe("Rejected");
    }

    [Fact]
    public async Task Inbox_DoneListsWhatTheCallerDecidedNewestFirstWithoutActions()
    {
        await SeedOrgAsync();
        var first = await SubmitPurchaseAsync("lina");
        var second = await SubmitPurchaseAsync("omar");
        await DecideAsync("karim", await OpenItemAsync("karim", first), "approve");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await DecideAsync("karim", await OpenItemAsync("karim", second), "reject", reason: "No");

        var done = await InboxAsync("karim", "Done");

        done.Select(i => i.GetProperty("request").GetProperty("id").GetGuid()).ShouldBe([second, first]);
        done.Select(i => i.GetProperty("outcome").GetString()).ShouldBe(["Rejected", "Approved"]);
        done.ShouldAllBe(i => i.GetProperty("actions").GetArrayLength() == 0);
    }

    [Fact]
    public async Task Inbox_OpenIsOldestFirstAndMatchesTheRequestsAvailableActions()
    {
        await SeedOrgAsync();
        var first = await SubmitPurchaseAsync("lina");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await SubmitLeaveAsync("omar");

        var open = await OpenItemsAsync("karim");

        open.Select(i => i.GetProperty("request").GetProperty("id").GetGuid()).ShouldBe([first, second]);
        using var karim = await ClientAsync("karim");
        var details = await JsonBody.ReadAsync(await karim.GetAsync(new Uri($"/api/requests/{first}", UriKind.Relative), Ct));
        var available = details.GetProperty("availableActions");
        Strings(available, "actions").ShouldBe(Strings(open[0], "actions"));
        available.GetProperty("version").GetString().ShouldBe(open[0].GetProperty("version").GetString());
    }

    // ---- Invalid transitions (§4) ----------------------------------------------------------------------------------

    [Fact]
    public async Task Approve_AnAlreadyDecidedTask_Returns409AlreadyDecided()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        (await DecideAsync("karim", item, "approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await DecideAsync("karim", item, "approve");

        await ProblemAssert.HasCode(response, 409, ErrorCodes.TaskAlreadyDecided);
    }

    [Theory]
    [InlineData("karim", "fulfill")]
    [InlineData("pete", "reject")]
    public async Task WrongActionForTheTaskKind_Returns409InvalidAction(string actor, string action)
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        if (actor == "pete")
        {
            await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");
            await DecideAsync("fiona", await OpenItemAsync("fiona", requestId), "approve");
        }

        using var response = await DecideAsync(actor, await OpenItemAsync(actor, requestId), action, reason: "No");

        await ProblemAssert.HasCode(response, 409, ErrorCodes.TaskInvalidAction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WithoutAReason_Returns400AndChangesNothing(string reason)
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");

        using var response = await DecideAsync("karim", await OpenItemAsync("karim", requestId), "reject", reason: reason);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        await ShouldBeAtAsync(requestId, "InReview", "ManagerApproval");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("stale")]
    public async Task Approve_WithAStaleVersion_Returns409AndChangesNothing(string version)
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        using var karim = await ClientAsync("karim");

        using var response = await karim.PostAsJsonAsync(TaskUri(item, "approve"), new { version }, Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.ConcurrencyConflict);
        await ShouldBeAtAsync(requestId, "InReview", "ManagerApproval");
    }

    // ---- Authorization (§3.3) --------------------------------------------------------------------------------------

    [Fact]
    public async Task FinanceOfficer_ApprovingTheirOwnPurchase_Returns403SelfDecisionAndItIsNotInTheirInbox()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("finny");
        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");
        var item = await OpenItemAsync("fiona", requestId);

        using var response = await DecideAsync("finny", item, "approve");

        await ProblemAssert.HasCode(response, 403, ErrorCodes.TaskSelfDecision);
        (await OpenItemsAsync("finny")).ShouldBeEmpty();
        (await NotificationsAsync(requestId)).ShouldNotContain(n => n.RecipientId == _users["finny"].Id && n.Type == NotificationType.TaskAssigned);
    }

    [Fact]
    public async Task VisibleButNotAssigned_Returns403NotEligible()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");

        // Karim still sees the request (direct manager) but the finance task isn't his.
        using var response = await DecideAsync("karim", await OpenItemAsync("fiona", requestId), "approve");

        await ProblemAssert.HasCode(response, 403, ErrorCodes.TaskNotEligible);
    }

    [Theory]
    [InlineData("omar")]
    [InlineData("hana")]
    [InlineData("ada")]
    public async Task NotVisible_Returns404(string actor)
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");

        using var response = await DecideAsync(actor, await OpenItemAsync("karim", requestId), "approve");

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
        await ShouldBeAtAsync(requestId, "InReview", "ManagerApproval");
    }

    [Theory]
    [InlineData("lina")]
    [InlineData("ada")]
    public async Task Inbox_WithoutTasksView_Returns403(string user)
    {
        await SeedOrgAsync();
        using var client = await ClientAsync(user);

        using var response = await client.GetAsync(new Uri("/api/tasks", UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task RoleQueue_SkipsInactiveMembersAndTheRequester()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("finny");

        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");

        (await NotificationsAsync(requestId)).Where(n => n.Type == NotificationType.TaskAssigned).Select(n => Name(n.RecipientId)).Order()
            .ShouldBe(["fiona", "karim"]);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Decision_OnADeactivatedRequestersRequest_StillAppliesButDoesNotNotifyThem(string action)
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        await SetActiveAsync("lina", isActive: false);

        using var response = await DecideAsync("karim", item, action, reason: action == "reject" ? "Not needed" : null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeAtAsync(requestId, action == "reject" ? "Rejected" : "InReview", action == "reject" ? "Closed" : "FinanceApproval", viewer: "karim");
        (await NotificationsAsync(requestId)).ShouldNotContain(n => n.RecipientId == _users["lina"].Id && n.Type != NotificationType.TaskAssigned,
            "an inactive requester gets no outcome notification (#56)");
        Factory.Realtime.Events.ShouldNotContain(e => e.Name == "notificationCreated" && e.Recipients.Contains(_users["lina"].Id));
    }

    [Fact]
    public async Task Decision_AfterTheRequesterIsReactivated_NotifiesThemAgain()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        await SetActiveAsync("lina", isActive: false);
        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");
        await SetActiveAsync("lina", isActive: true);

        await DecideAsync("fiona", await OpenItemAsync("fiona", requestId), "approve");

        (await NotificationsAsync(requestId)).Where(n => n.RecipientId == _users["lina"].Id).Select(n => n.Type)
            .ShouldBe([NotificationType.RequestApproved]);
    }

    [Fact]
    public async Task RoleMemberDeactivatedWithASessionStillOpen_Returns404()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");
        var item = await OpenItemAsync("fiona", requestId);
        using var fiona = await ClientAsync("fiona");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<FlowOps.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.FindByIdAsync(_users["fiona"].Id.ToString());
            user!.IsActive = false;
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }

        // Within the one-minute claim re-validation the session still works; the decision reads the database.
        using var response = await fiona.PostAsJsonAsync(TaskUri(item, "approve"), new { version = Version(item) }, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
        await ShouldBeAtAsync(requestId, "InReview", "FinanceApproval");
    }

    [Fact]
    public async Task NewManager_CannotDecideTheOldManagersTask_Returns403AndItStaysWithTheOldManager()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<FlowOps.Infrastructure.Identity.ApplicationUser>>();
            var lina = await users.FindByIdAsync(_users["lina"].Id.ToString());
            lina!.ManagerId = _users["omar"].Id;
            (await users.UpdateAsync(lina)).Succeeded.ShouldBeTrue();
        }

        using var response = await DecideAsync("omar", item, "approve");

        await ProblemAssert.HasCode(response, 403, ErrorCodes.TaskNotEligible);
        (await OpenItemAsync("karim", requestId)).GetProperty("taskId").GetGuid().ShouldBe(item.GetProperty("taskId").GetGuid());
    }

    [Fact]
    public async Task Inbox_ShowsOnlyTheCallersQueuesAndDecisions()
    {
        await SeedOrgAsync();
        var purchase = await SubmitPurchaseAsync("lina");
        await SubmitLeaveAsync("omar");
        await DecideAsync("karim", await OpenItemAsync("karim", purchase), "approve");

        (await OpenItemsAsync("pete")).ShouldBeEmpty();
        (await OpenItemsAsync("hana")).ShouldBeEmpty();
        (await InboxAsync("fiona", "Done")).ShouldBeEmpty();
        (await OpenItemsAsync("fiona")).Select(i => i.GetProperty("request").GetProperty("id").GetGuid()).ShouldBe([purchase]);
    }

    [Fact]
    public async Task Decide_OnAClosedRequest_Returns409RequestClosed()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        (await DecideAsync("karim", item, "reject", reason: "No")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await DecideAsync("karim", item, "approve");

        await ProblemAssert.HasCode(response, 409, ErrorCodes.RequestClosed);
    }

    [Fact]
    public async Task RejectLeave_NotifiesOnlyTheRequesterWithoutTheLeaveReason()
    {
        await SeedOrgAsync();
        var requestId = await SubmitLeaveAsync("lina");

        await DecideAsync("karim", await OpenItemAsync("karim", requestId), "reject", reason: "Team is short that week");

        var rejected = (await NotificationsAsync(requestId)).Where(n => n.Type == NotificationType.RequestRejected).ShouldHaveSingleItem();
        Name(rejected.RecipientId).ShouldBe("lina");
        rejected.Body.ShouldContain("Team is short that week");
        rejected.Body.ShouldNotContain(LeaveReason);
        Factory.Logs.Entries.ShouldNotContain(e => e.Mentions(LeaveReason) || e.Mentions("Team is short that week"));
    }

    [Theory]
    [InlineData("""{"comment":null}""")]
    [InlineData("""{"version":"123456789012345678901"}""")]
    public async Task Approve_WithoutAUsableVersion_Returns400(string json)
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        using var karim = await ClientAsync("karim");
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await karim.PostAsync(TaskUri(item, "approve"), content, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task Approve_WithATooLongComment_Returns400()
    {
        await SeedOrgAsync();
        var requestId = await SubmitPurchaseAsync("lina");
        var item = await OpenItemAsync("karim", requestId);
        using var karim = await ClientAsync("karim");

        using var response = await karim.PostAsJsonAsync(TaskUri(item, "approve"),
            new { version = Version(item), comment = new string('x', RequestLimits.CommentMaxLength + 1) }, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task Inbox_UnknownStatus_Returns400()
    {
        await SeedOrgAsync();
        using var karim = await ClientAsync("karim");

        using var response = await karim.GetAsync(new Uri("/api/tasks?status=Pending", UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    // ---- Concurrency (§3.4) ----------------------------------------------------------------------------------------

    [Fact]
    public async Task TwoFinanceOfficers_ApprovingAtOnce_ExactlyOneWins()
    {
        for (var run = 0; run < 20; run++)
        {
            await Factory.ResetAsync();
            await SeedOrgAsync(withSecondFinanceOfficer: true);
            var requestId = await SubmitPurchaseAsync("lina");
            await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");
            var item = await OpenItemAsync("fiona", requestId);
            using var fiona = await ClientAsync("fiona");
            using var fred = await ClientAsync("fred");

            var responses = await Task.WhenAll(
                fiona.PostAsJsonAsync(TaskUri(item, "approve"), new { version = Version(item) }, Ct),
                fred.PostAsJsonAsync(TaskUri(item, "approve"), new { version = Version(item) }, Ct));

            responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1, $"run {run}");
            // The loser either raced the save (xmin: concurrency.conflict) or loaded after the winner committed (already decided).
            var loser = await JsonBody.ReadAsync(responses.Single(r => r.StatusCode != HttpStatusCode.NoContent));
            loser.GetProperty("status").GetInt32().ShouldBe(409);
            loser.GetProperty("code").GetString().ShouldBeOneOf(ErrorCodes.ConcurrencyConflict, ErrorCodes.TaskAlreadyDecided);
            var tasks = await TasksAsync(requestId);
            tasks.Count(t => t.Stage == RequestStage.ProcurementFulfillment).ShouldBe(1);
            (await ActivityTypesAsync(requestId)).Count(t => t == "Approved").ShouldBe(2);
            foreach (var r in responses)
            {
                r.Dispose();
            }
        }
    }

    [Fact]
    public async Task ApproveAndReject_AtOnce_OneWinsAndTheRequestMatchesIt()
    {
        for (var run = 0; run < 20; run++)
        {
            await Factory.ResetAsync();
            await SeedOrgAsync(withSecondFinanceOfficer: true);
            var requestId = await SubmitPurchaseAsync("lina");
            await DecideAsync("karim", await OpenItemAsync("karim", requestId), "approve");
            var item = await OpenItemAsync("fiona", requestId);
            using var fiona = await ClientAsync("fiona");
            using var fred = await ClientAsync("fred");

            var responses = await Task.WhenAll(
                fiona.PostAsJsonAsync(TaskUri(item, "approve"), new { version = Version(item) }, Ct),
                fred.PostAsJsonAsync(TaskUri(item, "reject"), new { version = Version(item), reason = "No" }, Ct));

            responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1, $"run {run}");
            var approved = responses[0].StatusCode == HttpStatusCode.NoContent;
            await ShouldBeAtAsync(requestId, approved ? "Approved" : "Rejected", approved ? "ProcurementFulfillment" : "Closed");
            foreach (var r in responses)
            {
                r.Dispose();
            }
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private async Task SeedOrgAsync(bool withSecondFinanceOfficer = false)
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        _users = new Dictionary<string, TestUser>(StringComparer.Ordinal)
        {
            ["karim"] = karim,
            ["lina"] = await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory),
            ["omar"] = await Given.User("omar").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory),
            ["finny"] = await Given.User("finny").InDepartment(sales).ManagedBy(karim).WithRoles(Roles.Employee, Roles.FinanceOfficer).CreateAsync(Factory),
            ["fiona"] = await Given.User("fiona").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory),
            ["hana"] = await Given.User("hana").WithRoles(Roles.HrOfficer).CreateAsync(Factory),
            ["pete"] = await Given.User("pete").WithRoles(Roles.ProcurementOfficer).CreateAsync(Factory),
            ["ada"] = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory),
            ["gone"] = await Given.User("gone").WithRoles(Roles.FinanceOfficer).Inactive().CreateAsync(Factory),
        };
        if (withSecondFinanceOfficer)
        {
            _users["fred"] = await Given.User("fred").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);
        }
    }

    private async Task SetActiveAsync(string user, bool isActive)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<FlowOps.Infrastructure.Identity.ApplicationUser>>();
        var entity = await users.FindByIdAsync(_users[user].Id.ToString());
        entity!.IsActive = isActive;
        (await users.UpdateAsync(entity)).Succeeded.ShouldBeTrue();
    }

    private string Name(Guid id) => _users.Single(u => u.Value.Id == id).Key;

    private Task<HttpClient> ClientAsync(string user) => Factory.ClientFor(_users[user]);

    private async Task<Guid> SubmitPurchaseAsync(string requester)
    {
        using var client = await ClientAsync(requester);
        using var response = await client.PostAsJsonAsync(Purchase, new
        {
            itemName = "Laptop", description = "14 inch", estimatedCost = new { amount = "1250.500", currency = "JOD" }, justification = "Broken",
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> SubmitLeaveAsync(string requester)
    {
        using var client = await ClientAsync(requester);
        using var response = await client.PostAsJsonAsync(Leave, new
        {
            startDate = "2026-03-10", endDate = "2026-03-12", leaveType = "Annual", reason = LeaveReason,
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<List<JsonElement>> InboxAsync(string user, string status)
    {
        using var client = await ClientAsync(user);
        using var response = await client.GetAsync(new Uri($"/api/tasks?status={status}", UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. (await JsonBody.ReadAsync(response)).GetProperty("items").EnumerateArray()];
    }

    private Task<List<JsonElement>> OpenItemsAsync(string user) => InboxAsync(user, "Open");

    private async Task<JsonElement> OpenItemAsync(string user, Guid requestId) =>
        (await OpenItemsAsync(user)).Single(i => i.GetProperty("request").GetProperty("id").GetGuid() == requestId);

    private async Task<HttpResponseMessage> DecideAsync(string user, JsonElement item, string action, string? reason = null, string? note = null)
    {
        using var client = await ClientAsync(user);
        object body = action switch
        {
            "reject" => new { version = Version(item), reason = reason ?? "No" },
            "fulfill" => new { version = Version(item), note },
            _ => new { version = Version(item), comment = (string?)null },
        };
        return await client.PostAsJsonAsync(TaskUri(item, action), body, Ct);
    }

    private static Uri TaskUri(JsonElement item, string action) =>
        new($"/api/tasks/{item.GetProperty("taskId").GetGuid()}/{action}", UriKind.Relative);

    private static string Version(JsonElement item) => item.GetProperty("version").GetString()!;

    private static string[] Strings(JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(e => e.GetString()!)];

    /// <summary>Reads the request as <paramref name="viewer"/> (default: its requester, who must be active to sign in).</summary>
    private async Task ShouldBeAtAsync(Guid requestId, string status, string stage, string? viewer = null)
    {
        using var lina = await Factory.ClientFor(viewer is null ? await RequesterOfAsync(requestId) : _users[viewer]);
        var details = await JsonBody.ReadAsync(await lina.GetAsync(new Uri($"/api/requests/{requestId}", UriKind.Relative), Ct));
        details.GetProperty("status").GetString().ShouldBe(status);
        details.GetProperty("stage").GetString().ShouldBe(stage);
    }

    private async Task<string[]> ActivityTypesAsync(Guid requestId)
    {
        using var client = await Factory.ClientFor(await RequesterOfAsync(requestId));
        using var response = await client.GetAsync(new Uri($"/api/requests/{requestId}/activity", UriKind.Relative), Ct);
        return [.. (await JsonBody.ReadAsync(response)).EnumerateArray().Select(a => a.GetProperty("type").GetString()!)];
    }

    private async Task<TestUser> RequesterOfAsync(Guid requestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var requesterId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Requests
            .Where(r => r.Id == requestId).Select(r => r.RequesterId).SingleAsync(Ct);
        return _users.Values.Single(u => u.Id == requesterId);
    }

    private async Task<List<Notification>> NotificationsAsync(Guid requestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AsNoTracking()
            .Where(n => n.RequestId == requestId).ToListAsync(Ct);
    }

    private async Task<List<WorkTask>> TasksAsync(Guid requestId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Requests.AsNoTracking()
            .Where(r => r.Id == requestId).SelectMany(r => r.Tasks).ToListAsync(Ct);
    }
}
