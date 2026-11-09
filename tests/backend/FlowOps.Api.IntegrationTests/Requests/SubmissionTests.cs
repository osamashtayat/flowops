using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>BE-10: <c>POST /api/requests/purchase|leave</c> (API_CONTRACT.md §4, WORKFLOWS.md §2–3, TESTING.md §3.2).</summary>
public sealed class SubmissionTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly Uri Purchase = new("/api/requests/purchase", UriKind.Relative);
    private static readonly Uri Leave = new("/api/requests/leave", UriKind.Relative);

    // TestClock starts Mon 2026-03-02 09:00 Asia/Amman (06:00 UTC).
    private static readonly DateOnly Today = new(2026, 3, 2);

    [Fact]
    public async Task SubmitPurchase_Returns201DetailsAtManagerApproval()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonBody.ReadAsync(response);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldEndWith($"/api/requests/{id}");
        body.GetProperty("number").GetString().ShouldBe("FO-2026-000001");
        body.GetProperty("type").GetString().ShouldBe("EquipmentPurchase");
        body.GetProperty("title").GetString().ShouldBe("Laptop");
        body.GetProperty("status").GetString().ShouldBe("InReview");
        body.GetProperty("stage").GetString().ShouldBe("ManagerApproval");
        body.GetProperty("requester").GetProperty("id").GetGuid().ShouldBe(org.Lina.Id);
        body.GetProperty("department").GetProperty("id").GetGuid().ShouldBe(org.SalesId);
        body.GetProperty("submittedAt").GetDateTimeOffset().ShouldBe(Factory.Clock.GetUtcNow());
        body.GetProperty("closedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        var purchase = body.GetProperty("purchase");
        purchase.GetProperty("estimatedCost").GetProperty("amount").GetString().ShouldBe("1250.500");
        purchase.GetProperty("estimatedCost").GetProperty("currency").GetString().ShouldBe("JOD");
        body.GetProperty("leave").ValueKind.ShouldBe(JsonValueKind.Null);
        var task = body.GetProperty("tasks").EnumerateArray().Single();
        task.GetProperty("status").GetString().ShouldBe("Open");
        task.GetProperty("kind").GetString().ShouldBe("Approval");
        task.GetProperty("assignee").GetProperty("user").GetProperty("id").GetGuid().ShouldBe(org.Karim.Id);
        task.GetProperty("assignee").GetProperty("role").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("availableActions").ValueKind.ShouldBe(JsonValueKind.Null, "the requester can't decide their own request");
        body.GetProperty("canComment").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitPurchase_NotifiesTheManagerOnly()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);

        var notifications = await NotificationsAsync();
        var n = notifications.ShouldHaveSingleItem();
        n.RecipientId.ShouldBe(org.Karim.Id);
        n.Type.ShouldBe(NotificationType.TaskAssigned);
        n.RequestId.ShouldBe((await JsonBody.ReadAsync(response)).GetProperty("id").GetGuid());
        n.Title.ShouldContain("FO-2026-000001");
    }

    [Fact]
    public async Task SubmitLeave_Returns201WithReasonForTheRequester()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(8), Today.AddDays(10), reason: "Family visit"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonBody.ReadAsync(response);
        body.GetProperty("type").GetString().ShouldBe("Leave");
        body.GetProperty("title").GetString().ShouldBe("Annual leave · 10–12 Mar");
        var leave = body.GetProperty("leave");
        leave.GetProperty("startDate").GetString().ShouldBe("2026-03-10");
        leave.GetProperty("days").GetInt32().ShouldBe(3);
        leave.GetProperty("leaveType").GetString().ShouldBe("Annual");
        leave.GetProperty("reason").GetString().ShouldBe("Family visit");
        leave.GetProperty("reasonHidden").GetBoolean().ShouldBeFalse();
        body.GetProperty("purchase").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SubmitLeave_NotificationNeverContainsTheReason()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        (await client.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(8), Today.AddDays(10), reason: "Hospital appointment"), Ct)).Dispose();

        var n = (await NotificationsAsync()).ShouldHaveSingleItem();
        n.RecipientId.ShouldBe(org.Karim.Id);
        (n.Title + n.Body).ShouldNotContain("Hospital", Case.Insensitive);
        n.Body.ShouldContain("Annual leave · 10–12 Mar");
    }

    [Fact]
    public async Task Submit_NumbersAreSequentialAndUnique()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        var numbers = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);
            numbers.Add((await JsonBody.ReadAsync(response)).GetProperty("number").GetString());
        }

        numbers.ShouldBe(["FO-2026-000001", "FO-2026-000002", "FO-2026-000003"]);
    }

    [Fact]
    public async Task Submit_NumberUsesTheCompanyYear()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);
        // 2026-12-31 22:00 UTC is already 2027-01-01 01:00 in Amman (UTC+3).
        Factory.Clock.Advance(new DateTimeOffset(2026, 12, 31, 22, 0, 0, TimeSpan.Zero) - Factory.Clock.GetUtcNow());
        using var fresh = await Factory.ClientFor(org.Lina);

        using var response = await fresh.PostAsJsonAsync(Purchase, Laptop(), Ct);

        (await JsonBody.ReadAsync(response)).GetProperty("number").GetString().ShouldStartWith("FO-2027-");
    }

    [Fact]
    public async Task Submit_WithoutManager_Returns422AndPersistsNothing()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var lina = await Given.User("lina").InDepartment(sales).CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.RequestManagerNotAssigned);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task Submit_WithInactiveManager_Returns422()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").InDepartment(sales).Inactive().CreateAsync(Factory);
        var lina = await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(1), Today.AddDays(1)), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.RequestManagerNotAssigned);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task Submit_WithoutDepartment_Returns422()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var lina = await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.RequestDepartmentNotAssigned);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task Submit_IgnoresClientSuppliedRequesterDepartmentAndTime()
    {
        var org = await OrgAsync();
        var other = await Given.DepartmentAsync(Factory, "Finance", "FIN");
        using var client = await Factory.ClientFor(org.Lina);
        var body = new
        {
            itemName = "Laptop", description = "14 inch", justification = "Broken",
            estimatedCost = new { amount = "10", currency = "JOD" },
            requesterId = org.Karim.Id, departmentId = other.Id, department = new { id = other.Id },
            submittedAt = "2020-01-01T00:00:00Z", status = "Completed",
        };

        using var response = await client.PostAsJsonAsync(Purchase, body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await JsonBody.ReadAsync(response);
        created.GetProperty("requester").GetProperty("id").GetGuid().ShouldBe(org.Lina.Id);
        created.GetProperty("department").GetProperty("id").GetGuid().ShouldBe(org.SalesId);
        created.GetProperty("status").GetString().ShouldBe("InReview");
        created.GetProperty("submittedAt").GetDateTimeOffset().ShouldBe(Factory.Clock.GetUtcNow());
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0.001", true)]
    [InlineData("1000000", true)]
    [InlineData("1000000.000", true)]
    [InlineData("0", false)]
    [InlineData("0.000", false)]
    [InlineData("1000000.001", false)]
    [InlineData("1.0005", false)]
    [InlineData("-1", false)]
    [InlineData("1e3", false)]
    public async Task SubmitPurchase_AmountBoundaries(string amount, bool accepted)
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop() with { estimatedCost = new MoneyBody(amount, "JOD") }, Ct);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            return;
        }

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["estimatedCost.amount"]);
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    [InlineData(0, false)]
    public async Task SubmitPurchase_ItemNameLength(int length, bool accepted)
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop() with { itemName = new string('x', length) }, Ct);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        else
        {
            var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
            problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["itemName"]);
        }
    }

    [Fact]
    public async Task SubmitPurchase_WhitespaceOnlyText_Returns400OnTheField()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop() with { justification = "   " }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["justification"]);
    }

    [Fact]
    public async Task SubmitPurchase_OtherCurrency_Returns400()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop() with { estimatedCost = new MoneyBody("10", "USD") }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["estimatedCost.currency"]);
    }

    [Theory]
    [InlineData(0, 0, null, 1)]             // same day counts as one day
    [InlineData(0, 88, null, 89)]           // 89 days
    [InlineData(0, 89, null, 90)]           // 90 calendar days, counting both ends: the maximum
    [InlineData(0, 90, "endDate", 0)]       // 91 days
    [InlineData(-1, 0, "startDate", 0)]     // starts yesterday
    [InlineData(5, 4, "endDate", 0)]        // ends before it starts
    public async Task SubmitLeave_DateRules(int startOffset, int endOffset, string? failingField, int expectedDays)
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(startOffset), Today.AddDays(endOffset)), Ct);

        if (failingField is null)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
            (await JsonBody.ReadAsync(response)).GetProperty("leave").GetProperty("days").GetInt32().ShouldBe(expectedDays);
            return;
        }

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe([failingField]);
    }

    [Fact]
    public async Task SubmitLeave_TodayIsTheCompanyDateNotUtc()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);
        // 21:30 UTC on 2 March is already 00:30 on 3 March in Amman.
        Factory.Clock.Advance(TimeSpan.FromHours(15.5));
        using var fresh = await Factory.ClientFor(org.Lina);

        using var stillUtcToday = await fresh.PostAsJsonAsync(Leave, LeaveBody(Today, Today), Ct);
        using var companyToday = await fresh.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(1), Today.AddDays(1)), Ct);

        await ProblemAssert.HasCode(stillUtcToday, 400, ErrorCodes.Validation);
        companyToday.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public async Task SubmitLeave_ReasonLength(int length, bool accepted)
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var response = await client.PostAsJsonAsync(Leave, LeaveBody(Today, Today, reason: new string('r', length)), Ct);

        ((int)response.StatusCode).ShouldBe(accepted ? 201 : 400);
    }

    [Fact]
    public async Task SubmitLeave_UnknownLeaveTypeOrMissingDates_Returns400()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);

        using var unknownType = await client.PostAsJsonAsync(Leave, new { startDate = "2026-03-10", endDate = "2026-03-11", leaveType = "Vacation" }, Ct);
        using var missing = await client.PostAsJsonAsync(Leave, new { leaveType = "Annual" }, Ct);

        await ProblemAssert.HasCode(unknownType, 400, ErrorCodes.Validation);
        var problem = await ProblemAssert.HasCode(missing, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["startDate", "endDate"], ignoreOrder: true);
    }

    [Fact]
    public async Task Submit_WithoutEmployeeRole_Returns403()
    {
        var org = await OrgAsync();
        var fiona = await Given.User("fiona").ManagedBy(org.Karim).WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);
        using var client = await Factory.ClientFor(fiona);

        using var response = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task Submit_ByUserDeactivatedInsideTheRevalidationWindow_Returns401AndPersistsNothing()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);
        await ChangeUserAsync(org.Lina.Id, (user, _) => { user.IsActive = false; return Task.CompletedTask; });

        // Still inside the 1-minute claim re-validation: the cookie authenticates, the database says inactive (#77).
        using var purchase = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);
        using var leave = await client.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(1), Today.AddDays(1)), Ct);

        await ProblemAssert.HasCode(purchase, 401, ErrorCodes.AuthUnauthenticated);
        await ProblemAssert.HasCode(leave, 401, ErrorCodes.AuthUnauthenticated);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task Submit_ByUserWhoseEmployeeRoleWasJustRemoved_Returns403AndPersistsNothing()
    {
        var org = await OrgAsync();
        using var client = await Factory.ClientFor(org.Lina);
        await ChangeUserAsync(org.Lina.Id, async (user, users) =>
        {
            (await users.RemoveFromRoleAsync(user, Roles.Employee)).Succeeded.ShouldBeTrue();
            (await users.AddToRoleAsync(user, Roles.FinanceOfficer)).Succeeded.ShouldBeTrue();
        });

        using var purchase = await client.PostAsJsonAsync(Purchase, Laptop(), Ct);
        using var leave = await client.PostAsJsonAsync(Leave, LeaveBody(Today.AddDays(1), Today.AddDays(1)), Ct);

        await ProblemAssert.HasCode(purchase, 403, ErrorCodes.AuthForbidden);
        await ProblemAssert.HasCode(leave, 403, ErrorCodes.AuthForbidden);
        await AssertNothingPersistedAsync();
    }

    /// <summary>Changes the user straight in the store, as an admin's write would, without ending the caller's session.</summary>
    private async Task ChangeUserAsync(Guid userId, Func<ApplicationUser, UserManager<ApplicationUser>, Task> change)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        user.ShouldNotBeNull();
        await change(user, users);
        (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
    }

    private async Task<(TestUser Lina, TestUser Karim, Guid SalesId)> OrgAsync()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        var lina = await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory);
        return (lina, karim, sales.Id);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AsNoTracking().ToListAsync(Ct);
    }

    private async Task AssertNothingPersistedAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Requests.CountAsync(Ct)).ShouldBe(0);
        (await db.Notifications.CountAsync(Ct)).ShouldBe(0);
    }

    private static PurchaseBody Laptop() => new("Laptop", "14 inch developer laptop", new MoneyBody("1250.5", "JOD"), "Current one fails daily");

    private static object LeaveBody(DateOnly start, DateOnly end, string? reason = null) =>
        new { startDate = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
              endDate = end.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), leaveType = "Annual", reason };

    private sealed record MoneyBody(string amount, string currency);

    private sealed record PurchaseBody(string itemName, string description, MoneyBody estimatedCost, string justification);
}
