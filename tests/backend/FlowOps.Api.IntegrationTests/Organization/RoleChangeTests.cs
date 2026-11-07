using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FlowOps.Api.IntegrationTests.Organization.UsersApiTests;

namespace FlowOps.Api.IntegrationTests.Organization;

/// <summary>
/// ADR-0012: Administrator is only given to new accounts, and an admin's own role change waits for another
/// active admin (72 hours). Every other role edit by one admin stays immediate.
/// </summary>
public sealed class RoleChangeTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly Uri List = new("/api/admin/role-changes", UriKind.Relative);
    private static readonly string[] AdministratorOnly = [Roles.Administrator];

    // ---- Administrator only on new accounts -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateRoles_AddingAdministratorToAnExistingUser_Returns422AndKeepsRoles()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id, "/roles"), RoleSet("Administrator", "Employee"), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserAdminRoleOnCreateOnly);
        (await RolesOfAsync(admin, lina.Id)).ShouldBe(["Employee"]);
    }

    [Fact]
    public async Task Create_WithAdministrator_IsAllowed()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(new Uri("/api/users", UriKind.Relative), new
        {
            email = "nour@flowops.test", displayName = "Nour", initialPassword = "Initial-Pa55word!", departmentId = (Guid?)null,
            managerId = (Guid?)null, roles = AdministratorOnly,
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateRoles_OtherAdminKeepingAdministratorAndAddingARole_IsImmediate()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(bea.Id, "/roles"), RoleSet("Administrator", "FinanceOfficer"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RolesOfAsync(admin, bea.Id)).ShouldBe(["Administrator", "FinanceOfficer"]);
        (await PendingCountAsync()).ShouldBe(0);
    }

    // ---- Proposing your own change ----------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateRoles_OnSelf_Returns202PendingAndKeepsRoles()
    {
        var (ada, _) = await TwoAdminsAsync();
        using var client = await Factory.ClientFor(ada);

        using var response = await client.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "FinanceOfficer"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/admin/role-changes/{(await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid()}");
        var change = await JsonBody.ReadAsync(response);
        change.GetProperty("status").GetString().ShouldBe("Pending");
        change.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(ada.Id);
        Strings(change, "proposedRoles").ShouldBe(["Administrator", "FinanceOfficer"]);
        Strings(change, "previousRoles").ShouldBe(["Administrator"]);
        change.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(TestClock.Start.AddHours(72));
        Strings(change, "availableActions").ShouldBe(["cancel"]);
        (await RolesOfAsync(client, ada.Id)).ShouldBe(["Administrator"]);
    }

    [Fact]
    public async Task UpdateRoles_OnSelfWithUnchangedRoles_Returns200AndProposesNothing()
    {
        var (ada, _) = await TwoAdminsAsync();
        using var client = await Factory.ClientFor(ada);

        using var response = await client.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PendingCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateRoles_OnSelfAsTheOnlyAdmin_Returns422NoReviewer()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        await Given.User("cleo").WithRoles(Roles.Administrator).Inactive().CreateAsync(Factory);
        using var client = await Factory.ClientFor(ada);

        using var response = await client.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "Employee"), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.RoleChangeNoReviewer);
        (await PendingCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateRoles_OnSelfWhilePending_Returns409AlreadyPending()
    {
        var (ada, _) = await TwoAdminsAsync();
        using var client = await Factory.ClientFor(ada);
        await ProposeAsync(client, ada, "Administrator", "Employee");

        using var response = await client.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "HrOfficer"), Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.RoleChangeAlreadyPending);
    }

    [Fact]
    public async Task UpdateRoles_OnSelfAfterThePreviousOneExpired_ProposesANewOne()
    {
        var (ada, _) = await TwoAdminsAsync();
        string firstId;
        using (var client = await Factory.ClientFor(ada))
        {
            firstId = (await ProposeAsync(client, ada, "Administrator", "Employee")).GetProperty("id").GetString()!;
        }

        Factory.Clock.Advance(RoleChange.Lifetime);
        using var later = await Factory.ClientFor(ada);

        using var response = await later.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "HrOfficer"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var first = await JsonBody.ReadAsync(await later.GetAsync(ChangeUri(firstId), Ct));
        first.GetProperty("status").GetString().ShouldBe("Expired");
        first.GetProperty("decidedAt").GetDateTimeOffset().ShouldBe(TestClock.Start + RoleChange.Lifetime);
    }

    [Fact]
    public async Task UpdateRoles_OnSelf_NotifiesTheOtherActiveAdminsOnly()
    {
        var (ada, bea) = await TwoAdminsAsync();
        var carl = await Given.User("carl").WithRoles(Roles.Administrator).CreateAsync(Factory);
        await Given.User("cleo").WithRoles(Roles.Administrator).Inactive().CreateAsync(Factory);
        await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(ada);

        await ProposeAsync(client, ada, "Administrator", "Employee");

        var notifications = await NotificationsAsync();
        notifications.Select(n => n.RecipientId).Order().ShouldBe(new[] { bea.Id, carl.Id }.Order());
        notifications.ShouldAllBe(n => n.Type == NotificationType.RoleChangePending && n.RequestId == null && n.TaskId == null);
    }

    [Fact]
    public async Task UpdateRoles_OnSelfRemovingAdministratorAsTheLastAdmin_Returns422LastAdmin()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var client = await Factory.ClientFor(ada);

        using var response = await client.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Employee"), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserLastAdmin);
    }

    // ---- Deciding -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Approve_ByAnotherAdmin_AppliesTheRolesAndNotifiesTheProposer()
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Employee", "FinanceOfficer");
        using var beaClient = await Factory.ClientFor(bea);

        using var response = await DecideAsync(beaClient, change, "approve");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var approved = await JsonBody.ReadAsync(response);
        approved.GetProperty("status").GetString().ShouldBe("Approved");
        approved.GetProperty("decidedBy").GetProperty("id").GetGuid().ShouldBe(bea.Id);
        Strings(approved, "availableActions").ShouldBeEmpty();
        (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(["Employee", "FinanceOfficer"]);
        (await NotificationsAsync()).Where(n => n.RecipientId == ada.Id).ShouldHaveSingleItem().Type
            .ShouldBe(NotificationType.RoleChangeApproved);
        Factory.Logs.Entries.ShouldContain(e => e.Mentions(bea.Id.ToString()) && e.Mentions(change.GetProperty("id").GetString()!));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Decide_ByTheProposer_Returns422SelfApproval(string action)
    {
        var (ada, _) = await TwoAdminsAsync();
        using var client = await Factory.ClientFor(ada);
        var change = await ProposeAsync(client, ada, "Administrator", "Employee");

        using var response = await DecideAsync(client, change, action);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.RoleChangeSelfApproval);
        (await RolesOfAsync(client, ada.Id)).ShouldBe(["Administrator"]);
    }

    [Fact]
    public async Task Approve_AfterExpiry_Returns409NotPendingAndKeepsRoles()
    {
        var (ada, bea) = await TwoAdminsAsync();
        JsonElement change;
        using (var adaClient = await Factory.ClientFor(ada))
        {
            change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        }

        Factory.Clock.Advance(RoleChange.Lifetime);
        using var beaClient = await Factory.ClientFor(bea);

        using var response = await DecideAsync(beaClient, change, "approve");

        await ProblemAssert.HasCode(response, 409, ErrorCodes.RoleChangeNotPending);
        (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(["Administrator"]);
    }

    [Fact]
    public async Task Approve_Twice_Returns409NotPending()
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var beaClient = await Factory.ClientFor(bea);
        var approved = await JsonBody.ReadAsync(await DecideAsync(beaClient, change, "approve"));

        using var response = await DecideAsync(beaClient, approved, "approve");

        await ProblemAssert.HasCode(response, 409, ErrorCodes.RoleChangeNotPending);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("not-a-version")]
    public async Task Approve_WithAStaleVersion_Returns409Concurrency(string version)
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var beaClient = await Factory.ClientFor(bea);

        using var response = await beaClient.PostAsJsonAsync(ChangeUri(Id(change), "/approve"), new { version }, Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task Approve_AfterAnotherAdminChangedTheRolesDirectly_Returns409StaleAndKeepsThem()
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var beaClient = await Factory.ClientFor(bea);
        (await beaClient.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "HrOfficer"), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        using var response = await DecideAsync(beaClient, change, "approve");

        await ProblemAssert.HasCode(response, 409, ErrorCodes.RoleChangeStale);
        (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(["Administrator", "HrOfficer"]);
    }

    [Fact]
    public async Task Approve_ByAnAdminDemotedMomentsAgo_Returns403EvenWithAnAdminSession()
    {
        var (ada, bea) = await TwoAdminsAsync();
        var carl = await Given.User("carl").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var carlClient = await Factory.ClientFor(carl);
        using var beaClient = await Factory.ClientFor(bea);
        (await beaClient.PutAsJsonAsync(ById(carl.Id, "/roles"), RoleSet("Employee"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await DecideAsync(carlClient, change, "approve");

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
        (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(["Administrator"]);
    }

    [Fact]
    public async Task Approve_RemovingTheProposersAdministrator_LeavesTheApproverAsAdmin()
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Employee");
        using var beaClient = await Factory.ClientFor(bea);

        (await DecideAsync(beaClient, change, "approve")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(["Employee"]);
    }

    [Fact]
    public async Task Reject_KeepsTheRolesShowsTheReasonAndNotifiesWithoutIt()
    {
        const string reason = "Talk to finance first";
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "FinanceOfficer");
        using var beaClient = await Factory.ClientFor(bea);

        using var response = await beaClient.PostAsJsonAsync(ChangeUri(Id(change), "/reject"), new { version = Version(change), reason }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rejected = await JsonBody.ReadAsync(response);
        rejected.GetProperty("status").GetString().ShouldBe("Rejected");
        rejected.GetProperty("rejectionReason").GetString().ShouldBe(reason);
        (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(["Administrator"]);
        var notification = (await NotificationsAsync()).Where(n => n.RecipientId == ada.Id).ShouldHaveSingleItem();
        notification.Type.ShouldBe(NotificationType.RoleChangeRejected);
        $"{notification.Title} {notification.Body}".ShouldNotContain(reason);
        Factory.Logs.Entries.ShouldNotContain(e => e.Mentions(reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_WithoutAReason_Returns400(string reason)
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var beaClient = await Factory.ClientFor(bea);

        using var response = await beaClient.PostAsJsonAsync(ChangeUri(Id(change), "/reject"), new { version = Version(change), reason }, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task Cancel_ByTheProposer_ClosesItSoANewOneCanBeProposed()
    {
        var (ada, _) = await TwoAdminsAsync();
        using var client = await Factory.ClientFor(ada);
        var change = await ProposeAsync(client, ada, "Administrator", "Employee");

        using var response = await DecideAsync(client, change, "cancel");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(response)).GetProperty("status").GetString().ShouldBe("Cancelled");
        (await client.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "HrOfficer"), Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Cancel_ByAnotherAdmin_Returns403NotProposer()
    {
        var (ada, bea) = await TwoAdminsAsync();
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var beaClient = await Factory.ClientFor(bea);

        using var response = await DecideAsync(beaClient, change, "cancel");

        await ProblemAssert.HasCode(response, 403, ErrorCodes.RoleChangeNotProposer);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("cancel")]
    public async Task Decide_UnknownChange_Returns404(string action)
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(ChangeUri(Guid.CreateVersion7().ToString(), "/" + action),
            new { version = "1", reason = "x" }, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    // ---- Listing and access -----------------------------------------------------------------------------------------

    [Fact]
    public async Task List_PendingExcludesExpiredAndShowsTheCallersActions()
    {
        var (ada, bea) = await TwoAdminsAsync();
        using (var beaFirst = await Factory.ClientFor(bea))
        {
            await ProposeAsync(beaFirst, bea, "Administrator", "Employee");
        }

        Factory.Clock.Advance(RoleChange.Lifetime);
        using var adaClient = await Factory.ClientFor(ada);
        await ProposeAsync(adaClient, ada, "Administrator", "HrOfficer");
        using var beaClient = await Factory.ClientFor(bea);

        var pending = await JsonBody.ReadAsync(await beaClient.GetAsync(new Uri(List + "?status=Pending", UriKind.Relative), Ct));
        var expired = await JsonBody.ReadAsync(await beaClient.GetAsync(new Uri(List + "?status=Expired", UriKind.Relative), Ct));
        var all = await JsonBody.ReadAsync(await beaClient.GetAsync(List, Ct));

        var item = pending.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("user").GetProperty("id").GetGuid().ShouldBe(ada.Id);
        Strings(item, "availableActions").ShouldBe(["approve", "reject"]);
        expired.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("user").GetProperty("id").GetGuid().ShouldBe(bea.Id);
        all.GetProperty("totalCount").GetInt32().ShouldBe(2);
        all.GetProperty("items")[0].GetProperty("user").GetProperty("id").GetGuid().ShouldBe(ada.Id);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/approve")]
    public async Task NonAdmin_Returns403(string method, string suffix)
    {
        var (ada, _) = await TwoAdminsAsync();
        var lina = await Given.User("lina").WithRoles(Roles.Employee, Roles.HrOfficer).CreateAsync(Factory);
        using var adaClient = await Factory.ClientFor(ada);
        var change = await ProposeAsync(adaClient, ada, "Administrator", "Employee");
        using var client = await Factory.ClientFor(lina);

        using var response = method == "GET"
            ? await client.GetAsync(ChangeUri(Id(change)), Ct)
            : await client.PostAsJsonAsync(ChangeUri(Id(change), suffix), new { version = Version(change) }, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    // ---- Concurrency --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ApproveAndCancel_Racing_HaveExactlyOneWinnerAndConsistentRoles()
    {
        for (var run = 0; run < 20; run++)
        {
            await Factory.ResetAsync();
            var (ada, bea) = await TwoAdminsAsync();
            using var adaClient = await Factory.ClientFor(ada);
            using var beaClient = await Factory.ClientFor(bea);
            var change = await ProposeAsync(adaClient, ada, "Administrator", "FinanceOfficer");

            var responses = await Task.WhenAll(DecideAsync(beaClient, change, "approve"), DecideAsync(adaClient, change, "cancel"));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1, $"run {run}");
            responses.Single(r => r.StatusCode != HttpStatusCode.OK).StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var approved = responses[0].StatusCode == HttpStatusCode.OK;
            (await RolesOfAsync(beaClient, ada.Id)).ShouldBe(approved ? ["Administrator", "FinanceOfficer"] : ["Administrator"]);
            var status = (await JsonBody.ReadAsync(await beaClient.GetAsync(ChangeUri(Id(change)), Ct))).GetProperty("status").GetString();
            status.ShouldBe(approved ? "Approved" : "Cancelled");
            foreach (var r in responses)
            {
                r.Dispose();
            }
        }
    }

    [Fact]
    public async Task OwnProposals_Racing_LeaveExactlyOnePending()
    {
        for (var run = 0; run < 20; run++)
        {
            await Factory.ResetAsync();
            var (ada, _) = await TwoAdminsAsync();
            using var first = await Factory.ClientFor(ada);
            using var second = await Factory.ClientFor(ada);

            var responses = await Task.WhenAll(
                first.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "Employee"), Ct),
                second.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "HrOfficer"), Ct));

            responses.Count(r => r.StatusCode == HttpStatusCode.Accepted).ShouldBe(1, $"run {run}");
            await ProblemAssert.HasCode(responses.Single(r => r.StatusCode != HttpStatusCode.Accepted), 409, ErrorCodes.RoleChangeAlreadyPending);
            (await PendingCountAsync()).ShouldBe(1);
            foreach (var r in responses)
            {
                r.Dispose();
            }
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------------

    private async Task<(TestUser Ada, TestUser Bea)> TwoAdminsAsync()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        return (ada, bea);
    }

    private static async Task<JsonElement> ProposeAsync(HttpClient client, TestUser self, params string[] roles)
    {
        using var response = await client.PutAsJsonAsync(ById(self.Id, "/roles"), RoleSet(roles), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return await JsonBody.ReadAsync(response);
    }

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, JsonElement change, string action) =>
        client.PostAsJsonAsync(ChangeUri(Id(change), "/" + action), new { version = Version(change), reason = "Not now" }, Ct);

    private static Uri ChangeUri(string id, string suffix = "") => new($"/api/admin/role-changes/{id}{suffix}", UriKind.Relative);

    private static string Id(JsonElement change) => change.GetProperty("id").GetString()!;

    private static string Version(JsonElement change) => change.GetProperty("version").GetString()!;

    private static object RoleSet(params string[] roles) => new { roles };

    private static string[] Strings(JsonElement element, string property) =>
        [.. element.GetProperty(property).EnumerateArray().Select(e => e.GetString()!)];

    private static async Task<string[]> RolesOfAsync(HttpClient admin, Guid userId) =>
        Strings(await JsonBody.ReadAsync(await admin.GetAsync(ById(userId), Ct)), "roles");

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AsNoTracking().ToListAsync(Ct);
    }

    private async Task<int> PendingCountAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RoleChanges
            .CountAsync(c => c.Status == RoleChangeStatus.Pending, Ct);
    }
}
