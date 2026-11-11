using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Common;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowOps.Api.IntegrationTests.Persistence;

/// <summary>BE-07: the aggregate survives a round trip through PostgreSQL, and the database backs the invariants it can know.</summary>
public sealed class DomainMappingTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly TimeZoneInfo Amman = TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman");

    [Fact]
    public async Task PurchaseAggregate_RoundTripsWithDetailsTasksAndActivities()
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));

        var loaded = await LoadAsync(submitted.Id);

        loaded.Number.ShouldBe(submitted.Number);
        loaded.Type.ShouldBe(RequestType.EquipmentPurchase);
        loaded.RequesterId.ShouldBe(org.Requester);
        loaded.DepartmentId.ShouldBe(org.Department);
        loaded.Status.ShouldBe(RequestStatus.InReview);
        loaded.Stage.ShouldBe(RequestStage.ManagerApproval);
        loaded.SubmittedAt.ShouldBe(TestClock.Start);
        loaded.Version.ShouldNotBe(0u);
        loaded.Leave.ShouldBeNull();
        loaded.Purchase!.ItemName.ShouldBe("Laptop");
        loaded.Purchase.EstimatedCost.ShouldBe(Money.Create(1250.500m, SupportedCurrencies.Jod));
        var task = loaded.Tasks.ShouldHaveSingleItem();
        (task.Kind, task.Stage, task.AssigneeUserId, task.Status).ShouldBe((WorkTaskKind.Approval, RequestStage.ManagerApproval, org.Manager, WorkTaskStatus.Open));
        loaded.Activities.Select(a => a.Type).ShouldBe([ActivityType.Submitted, ActivityType.TaskAssigned], ignoreOrder: true);
    }

    [Fact]
    public async Task LeaveAggregate_RoundTripsDatesTypeAndReason()
    {
        var org = await SeedOrgAsync();
        var leave = LeaveDetails.Create(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12), LeaveType.Sick, "Surgery", TestClock.Start, Amman);
        var submitted = await SaveAsync(Request.SubmitLeave(await NextNumberAsync(), org.Requester, org.Department, leave, _ => Assignee.ForUser(org.Manager), TestClock.Start));

        var loaded = await LoadAsync(submitted.Id);

        loaded.Purchase.ShouldBeNull();
        (loaded.Leave!.StartDate, loaded.Leave.EndDate, loaded.Leave.LeaveType, loaded.Leave.Reason)
            .ShouldBe((new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12), LeaveType.Sick, "Surgery"));
    }

    [Fact]
    public async Task Decision_PersistsNextTaskAndChangesVersion()
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));
        var versionBefore = (await LoadAsync(submitted.Id)).Version;

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await db.Requests.Include(r => r.Tasks).Include(r => r.Activities).SingleAsync(r => r.Id == submitted.Id, Ct);
            request.DecideTask(request.OpenTask!.Id, org.Manager, TaskDecision.Approve("ok"), _ => Assignee.ForRole(Roles.FinanceOfficer), TestClock.Start.AddHours(1));
            await db.SaveChangesAsync(Ct);
        }

        var loaded = await LoadAsync(submitted.Id);
        loaded.Version.ShouldNotBe(versionBefore);
        loaded.Stage.ShouldBe(RequestStage.FinanceApproval);
        loaded.Tasks.Count.ShouldBe(2);
        loaded.OpenTask!.AssigneeRole.ShouldBe(Roles.FinanceOfficer);
        var decided = loaded.Tasks.Single(t => !t.IsOpen);
        (decided.Outcome, decided.DecidedById, decided.Comment).ShouldBe((WorkTaskOutcome.Approved, org.Manager, "ok"));
    }

    [Fact]
    public async Task Notification_RoundTripsAndReadState()
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));
        var notification = Notification.Create(
            org.Manager, NotificationType.TaskAssigned, "New approval", "FO request needs you", submitted.Id, submitted.OpenTask!.Id, TestClock.Start);
        notification.MarkRead(TestClock.Start.AddMinutes(5));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(Ct);
        }

        await using var readScope = Factory.Services.CreateAsyncScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.SingleAsync(n => n.Id == notification.Id, Ct);

        (loaded.RecipientId, loaded.Type, loaded.Title, loaded.RequestId, loaded.TaskId)
            .ShouldBe((org.Manager, NotificationType.TaskAssigned, "New approval", (Guid?)submitted.Id, submitted.OpenTask!.Id));
        loaded.ReadAt.ShouldBe(TestClock.Start.AddMinutes(5));
    }

    [Fact]
    public async Task SaveWithStaleVersion_ThrowsConcurrencyException()
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));
        await using var scopeA = Factory.Services.CreateAsyncScope();
        await using var scopeB = Factory.Services.CreateAsyncScope();
        var a = await LoadTrackedAsync(scopeA, submitted.Id);
        var b = await LoadTrackedAsync(scopeB, submitted.Id);

        Decide(a.Request, org, TaskDecision.Approve(null));
        await a.Db.SaveChangesAsync(Ct);
        Decide(b.Request, org, TaskDecision.Reject("No budget"));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => b.Db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task SecondOpenTaskForARequest_IsRejectedByTheDatabase()
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));

        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
            $"""
            INSERT INTO work_tasks (id, request_id, kind, stage, assignee_role, status, created_at)
            VALUES ('{Guid.CreateVersion7()}', '{submitted.Id}', 'Approval', 'FinanceApproval', 'FinanceOfficer', 'Open', now())
            """));

        (error.SqlState, error.ConstraintName).ShouldBe((PostgresErrorCodes.UniqueViolation, "ux_work_tasks_open_per_request"));
    }

    [Theory]
    [InlineData("assignee_user_id = NULL", "ck_work_tasks_one_assignee")]
    [InlineData("assignee_role = 'FinanceOfficer'", "ck_work_tasks_one_assignee")]
    [InlineData("outcome = 'Approved'", "ck_work_tasks_decision_iff_done")]
    [InlineData("kind = 'Review'", "ck_work_tasks_kind")]
    public async Task InvalidTaskRow_IsRejectedByCheckConstraint(string assignment, string constraint)
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));

        var error = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync($"UPDATE work_tasks SET {assignment} WHERE request_id = '{submitted.Id}'"));

        (error.SqlState, error.ConstraintName).ShouldBe((PostgresErrorCodes.CheckViolation, constraint));
    }

    [Theory]
    [InlineData("estimated_cost_currency = 'USD'", "ck_purchase_request_details_currency")]
    [InlineData("estimated_cost_amount = 0", "ck_purchase_request_details_amount")]
    public async Task InvalidMoney_IsRejectedByCheckConstraint(string assignment, string constraint)
    {
        var org = await SeedOrgAsync();
        var submitted = await SaveAsync(await NewPurchaseAsync(org));

        var error = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync($"UPDATE purchase_request_details SET {assignment} WHERE request_id = '{submitted.Id}'"));

        (error.SqlState, error.ConstraintName).ShouldBe((PostgresErrorCodes.CheckViolation, constraint));
    }

    [Fact]
    public async Task CurrencyConstraint_AllowsExactlyTheSupportedCurrencies()
    {
        var definition = await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_purchase_request_details_currency'");

        foreach (var currency in SupportedCurrencies.All)
        {
            definition.ShouldContain($"'{currency}'");
        }

        (definition.Split('\'').Length / 2).ShouldBe(SupportedCurrencies.All.Count);
    }

    [Fact]
    public async Task Schema_HasTheIndexesConstraintsAndSequenceFromTheDomainModel()
    {
        var openTaskIndex = await ScalarAsync<string>("SELECT indexdef FROM pg_indexes WHERE indexname = 'ux_work_tasks_open_per_request'");
        openTaskIndex.ShouldContain("UNIQUE INDEX");
        openTaskIndex.ShouldContain("WHERE ((status)::text = 'Open'::text)");

        var indexes = await ListAsync("SELECT indexname FROM pg_indexes WHERE schemaname = 'public'");
        indexes.ShouldContain("ix_requests_requester_id_submitted_at");
        indexes.ShouldContain("ix_requests_status");
        indexes.ShouldContain("ux_requests_number");
        indexes.ShouldContain("ix_work_tasks_open_assignee_user_id");
        indexes.ShouldContain("ix_work_tasks_open_assignee_role");
        indexes.ShouldContain("ix_notifications_recipient_id_created_at");
        indexes.ShouldContain("ix_notifications_unread_recipient_id");
        indexes.ShouldContain("ix_request_activities_request_id_occurred_at");

        var checks = await ListAsync("SELECT constraint_name FROM information_schema.check_constraints WHERE constraint_schema = 'public'");
        checks.ShouldContain("ck_work_tasks_one_assignee");
        checks.ShouldContain("ck_users_manager_not_self");
        checks.ShouldContain("ck_requests_status");

        (await ScalarAsync<long>("SELECT count(*) FROM pg_sequences WHERE sequencename = 'request_number_seq'")).ShouldBe(1);
    }

    private sealed record Org(Guid Requester, Guid Manager, Guid Department);

    private async Task<Org> SeedOrgAsync()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        var lina = await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory);
        await Given.User("fatima").WithRoles(Roles.Employee, Roles.FinanceOfficer).CreateAsync(Factory);
        return new Org(lina.Id, karim.Id, sales.Id);
    }

    private async Task<Request> NewPurchaseAsync(Org org)
    {
        var details = PurchaseDetails.Create("Laptop", "14-inch", Money.Create(1250.500m, SupportedCurrencies.Jod), "Broken screen");
        return Request.SubmitPurchase(await NextNumberAsync(), org.Requester, org.Department, details, _ => Assignee.ForUser(org.Manager), TestClock.Start);
    }

    private async Task<RequestNumber> NextNumberAsync() =>
        RequestNumber.Create(2026, await ScalarAsync<long>($"SELECT nextval('{AppDbContext.RequestNumberSequence}')"));

    private static void Decide(Request request, Org org, TaskDecision decision) =>
        request.DecideTask(request.OpenTask!.Id, org.Manager, decision, _ => Assignee.ForRole(Roles.FinanceOfficer), TestClock.Start.AddHours(1));

    private async Task<Request> SaveAsync(Request request)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Requests.Add(request);
        await db.SaveChangesAsync(Ct);
        return request;
    }

    private async Task<Request> LoadAsync(Guid id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await LoadTrackedAsync(scope, id)).Request;
    }

    private static async Task<(AppDbContext Db, Request Request)> LoadTrackedAsync(AsyncServiceScope scope, Guid id)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = await db.Requests.Include(r => r.Tasks).Include(r => r.Activities).SingleAsync(r => r.Id == id, Ct);
        return (db, request);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task<List<string>> ListAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var values = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
