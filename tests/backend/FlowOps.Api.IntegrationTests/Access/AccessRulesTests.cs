using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Domain.Errors;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Access;

/// <summary>
/// AUTHORIZATION.md §4 as tables over <see cref="AccessOrg"/>, evaluated by PostgreSQL (BE-09).
/// Each table is one test so the org is seeded once; a failure lists every wrong cell.
/// </summary>
public sealed class AccessRulesTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    /// <summary>Which requests each user sees, and by which rule.</summary>
    private static readonly Dictionary<string, string[]> Visible = new(StringComparer.Ordinal)
    {
        ["lina"] = ["P1", "P2", "P3", "L1"],          // R1 requester
        ["karim"] = ["P1", "P2", "P3", "L1", "PF"],   // R2 direct manager (+ R3 decided)
        ["boss"] = [],                                // second-level manager: not R2
        ["omar"] = [],                                // colleague
        ["fiona"] = ["P2", "P3", "PF"],               // R3 finance role queue (+ decided P3); not P1 (manager stage only)
        ["finny"] = ["P2", "P3", "PF"],               // R1 for PF, R3 finance for P2/P3
        ["hana"] = ["L1"],                            // R3 HR role queue: leave only
        ["pete"] = ["P3"],                            // R3 procurement: fulfilment stage only, not P2 at finance
        ["ada"] = [],                                 // Administrator grants no visibility
        ["gone"] = [],                                // inactive
    };

    /// <summary>(user, request) → expected eligibility failure code on that request's open task, or null.</summary>
    private static readonly (string User, string Request, string? Code)[] Eligibility =
    [
        ("karim", "P1", null),                         // T1 assigned manager
        ("boss", "P1", ErrorCodes.TaskNotEligible),    // manager's manager
        ("lina", "P1", ErrorCodes.TaskNotEligible),    // requester, not assigned: not_eligible comes first
        ("fiona", "P2", null),                         // T2 role queue
        ("finny", "P2", null),
        ("hana", "P2", ErrorCodes.TaskNotEligible),    // wrong role
        ("ada", "P2", ErrorCodes.TaskNotEligible),
        ("gone", "P2", ErrorCodes.TaskNotEligible),    // inactive holder of the role
        ("fiona", "PF", null),
        ("finny", "PF", ErrorCodes.TaskSelfDecision),  // I2: own request
        ("hana", "L1", null),
        ("fiona", "L1", ErrorCodes.TaskNotEligible),
        ("pete", "P3", null),
        ("fiona", "P3", ErrorCodes.TaskNotEligible),   // decided the finance step; the open step isn't hers
    ];

    private static readonly Dictionary<string, string[]> Decidable = new(StringComparer.Ordinal)
    {
        ["karim"] = ["P1"],
        ["fiona"] = ["P2", "PF"],
        ["finny"] = ["P2"],
        ["hana"] = ["L1"],
        ["pete"] = ["P3"],
        ["lina"] = [],
        ["boss"] = [],
        ["ada"] = [],
        ["gone"] = [],
    };

    [Fact]
    public async Task VisibleTo_MatchesTheVisibilityTable()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var wrong = new List<string>();

        foreach (var (user, expected) in Visible)
        {
            var me = await rules.ContextForAsync(org.Users[user], Ct);
            var ids = await rules.VisibleTo(db.Requests, me).Select(r => r.Id).ToListAsync(Ct);
            var actual = ids.Select(org.RequestName).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            {
                wrong.Add($"{user}: expected [{string.Join(",", expected)}] but saw [{string.Join(",", actual)}]");
            }
        }

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task CanViewAsync_AgreesWithTheListFilter()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (_, rules) = Services(scope);
        var wrong = new List<string>();

        foreach (var (user, visible) in Visible)
        {
            var me = await rules.ContextForAsync(org.Users[user], Ct);
            foreach (var (name, id) in org.Requests)
            {
                if (await rules.CanViewAsync(id, me, Ct) != visible.Contains(name))
                {
                    wrong.Add($"{user}/{name}");
                }
            }
        }

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task ViewerIds_AreExactlyTheUsersWhoCanViewEachRequest()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (_, rules) = Services(scope);
        var wrong = new List<string>();

        foreach (var (name, id) in org.Requests)
        {
            var expected = Visible.Where(v => v.Value.Contains(name)).Select(v => v.Key).Order(StringComparer.Ordinal).ToArray();
            var actual = (await rules.ViewerIds(id).ToListAsync(Ct))
                .Select(u => org.Users.Single(x => x.Value == u).Key).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                wrong.Add($"{name}: expected [{string.Join(",", expected)}] but got [{string.Join(",", actual)}]");
            }
        }

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task VisibleTo_SecondLevelManagerLosesAndGainsR2WithTheManagerLink()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using (var arrange = Factory.Services.CreateAsyncScope())
        {
            // lina now reports to boss directly.
            await arrange.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == org.Users["lina"])
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, org.Users["boss"]), Ct);
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var (_, rules) = Services(scope);
        var boss = await rules.ContextForAsync(org.Users["boss"], Ct);
        var karim = await rules.ContextForAsync(org.Users["karim"], Ct);

        (await rules.CanViewAsync(org.Requests["P1"], boss, Ct)).ShouldBeTrue("R2 follows the current manager link");
        (await rules.CanViewAsync(org.Requests["P1"], karim, Ct)).ShouldBeTrue("karim still holds P1's open manager task (R3)");
        (await rules.CanViewAsync(org.Requests["L1"], karim, Ct)).ShouldBeTrue("karim decided L1's manager step (R3)");
    }

    [Fact]
    public async Task VisibleTo_IsOneSqlStatement()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var me = await rules.ContextForAsync(org.Users["fiona"], Ct);

        var sql = rules.VisibleTo(db.Requests, me).ToQueryString();

        sql.ShouldContain("EXISTS");
        sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(s => s.Contains("SELECT", StringComparison.Ordinal)).ShouldBe(1, sql);
    }

    [Fact]
    public async Task ContextFor_ReadsRolesAndActiveFlagFromTheDatabase()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (_, rules) = Services(scope);

        var finny = await rules.ContextForAsync(org.Users["finny"], Ct);
        var gone = await rules.ContextForAsync(org.Users["gone"], Ct);
        var unknown = await rules.ContextForAsync(Guid.CreateVersion7(), Ct);

        finny.Roles.Order(StringComparer.Ordinal).ShouldBe(["Employee", "FinanceOfficer"]);
        finny.IsActive.ShouldBeTrue();
        gone.IsActive.ShouldBeFalse();
        unknown.IsActive.ShouldBeFalse();
        unknown.Roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task FailureCode_MatchesTheEligibilityTable()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var wrong = new List<string>();

        foreach (var (user, request, code) in Eligibility)
        {
            var me = await rules.ContextForAsync(org.Users[user], Ct);
            var loaded = await db.Requests.Include(r => r.Tasks).SingleAsync(r => r.Id == org.Requests[request], Ct);
            var actual = TaskEligibility.FailureCode(loaded.RequesterId, loaded.OpenTask!, me);
            if (actual != code)
            {
                wrong.Add($"{user}/{request}: expected {code ?? "eligible"} but got {actual ?? "eligible"}");
            }
        }

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnsureCanDecide_ThrowsForbiddenWithTheCode()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var finny = await rules.ContextForAsync(org.Users["finny"], Ct);
        var hana = await rules.ContextForAsync(org.Users["hana"], Ct);
        var pf = await db.Requests.Include(r => r.Tasks).SingleAsync(r => r.Id == org.Requests["PF"], Ct);

        Should.Throw<ForbiddenException>(() => TaskEligibility.EnsureCanDecide(pf.RequesterId, pf.OpenTask!, finny)).Code.ShouldBe(ErrorCodes.TaskSelfDecision);
        Should.Throw<ForbiddenException>(() => TaskEligibility.EnsureCanDecide(pf.RequesterId, pf.OpenTask!, hana)).Code.ShouldBe(ErrorCodes.TaskNotEligible);
    }

    [Fact]
    public async Task DecidableBy_MatchesTheInMemoryCheckForEveryUser()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var all = await db.Requests.Include(r => r.Tasks).AsNoTracking().ToListAsync(Ct);
        var wrong = new List<string>();

        foreach (var (user, expected) in Decidable)
        {
            var me = await rules.ContextForAsync(org.Users[user], Ct);
            var fromSql = await TaskEligibility.DecidableBy(db.Requests, me).Select(d => d.Request.Id).ToListAsync(Ct);
            var inMemory = all.Where(r => TaskEligibility.FailureCode(r.RequesterId, r.OpenTask!, me) is null).Select(r => r.Id);
            var sqlNames = fromSql.Select(org.RequestName).Order(StringComparer.Ordinal).ToArray();
            var memoryNames = inMemory.Select(org.RequestName).Order(StringComparer.Ordinal).ToArray();
            if (!sqlNames.SequenceEqual(expected.Order(StringComparer.Ordinal)) || !memoryNames.SequenceEqual(sqlNames))
            {
                wrong.Add($"{user}: expected [{string.Join(",", expected)}], SQL [{string.Join(",", sqlNames)}], memory [{string.Join(",", memoryNames)}]");
            }
        }

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task AvailableActions_FollowEligibilityAndTaskKind()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        async Task<AvailableActionsDto?> For(string user, string request)
        {
            var loaded = await db.Requests.Include(r => r.Tasks).SingleAsync(r => r.Id == org.Requests[request], Ct);
            return TaskEligibility.AvailableActions(loaded.RequesterId, loaded.OpenTask, await rules.ContextForAsync(org.Users[user], Ct));
        }

        var karimP1 = (await For("karim", "P1")).ShouldNotBeNull();
        karimP1.Actions.ShouldBe([TaskAction.Approve, TaskAction.Reject]);
        VersionToken.TryParse(karimP1.Version, out var version).ShouldBeTrue();
        version.ShouldBeGreaterThan(0u);
        (await For("pete", "P3")).ShouldNotBeNull().Actions.ShouldBe([TaskAction.Fulfill]);
        (await For("finny", "PF")).ShouldBeNull("own request");
        (await For("lina", "P1")).ShouldBeNull("requester");
        (await For("gone", "P2")).ShouldBeNull("inactive");
    }

    [Fact]
    public async Task DecidableBy_ProjectsTaskAndRequestInOneQuery()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var fiona = await rules.ContextForAsync(org.Users["fiona"], Ct);

        var rows = await TaskEligibility.DecidableBy(db.Requests, fiona)
            .OrderBy(d => d.Task.CreatedAt).ThenBy(d => d.Task.Id)
            .Select(d => new { TaskId = d.Task.Id, Number = d.Request.Number, d.Request.RequesterId })
            .ToListAsync(Ct);

        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(r => r.TaskId != Guid.Empty && r.Number.Value.StartsWith("FO-2026-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InactiveManagerAndInactiveRequester_SeeNothing()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await SetActiveAsync(org.Users["karim"], false);
        await SetActiveAsync(org.Users["lina"], false);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);

        foreach (var user in new[] { "karim", "lina" })
        {
            var me = await rules.ContextForAsync(org.Users[user], Ct);
            (await rules.VisibleTo(db.Requests, me).CountAsync(Ct)).ShouldBe(0, user);
            (await TaskEligibility.DecidableBy(db.Requests, me).CountAsync(Ct)).ShouldBe(0, user);
        }
    }

    [Fact]
    public async Task RoleRemovedInTheDatabase_TakesEffectImmediately()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using (var arrange = Factory.Services.CreateAsyncScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.UserRoles.Where(r => r.UserId == org.Users["hana"]).ExecuteDeleteAsync(Ct);
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var (dbContext, rules) = Services(scope);
        var hana = await rules.ContextForAsync(org.Users["hana"], Ct);

        hana.Roles.ShouldBeEmpty();
        (await rules.CanViewAsync(org.Requests["L1"], hana, Ct)).ShouldBeFalse();
        (await TaskEligibility.DecidableBy(dbContext.Requests, hana).CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task FormerManager_StaysEligibleForTheOpenManagerTask()
    {
        // WORKFLOWS.md: no reassignment in Phase 1, so a manager change leaves the open T1 with the old manager.
        var org = await AccessOrg.SeedAsync(Factory);
        await using (var arrange = Factory.Services.CreateAsyncScope())
        {
            await arrange.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == org.Users["lina"])
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, org.Users["boss"]), Ct);
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var p1 = await db.Requests.Include(r => r.Tasks).SingleAsync(r => r.Id == org.Requests["P1"], Ct);

        TaskEligibility.FailureCode(p1.RequesterId, p1.OpenTask!, await rules.ContextForAsync(org.Users["karim"], Ct)).ShouldBeNull();
        TaskEligibility.FailureCode(p1.RequesterId, p1.OpenTask!, await rules.ContextForAsync(org.Users["boss"], Ct))
            .ShouldBe(ErrorCodes.TaskNotEligible);
    }

    [Fact]
    public async Task FailureCode_OnADecidedTaskOfTheAssignee_IsEligibleSoTheDomainAnswers409()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var p3 = await db.Requests.Include(r => r.Tasks).SingleAsync(r => r.Id == org.Requests["P3"], Ct);
        var financeTask = p3.Tasks.Single(t => t.Stage == FlowOps.Domain.Requests.RequestStage.FinanceApproval);
        var fiona = await rules.ContextForAsync(org.Users["fiona"], Ct);

        TaskEligibility.FailureCode(p3.RequesterId, financeTask, fiona).ShouldBeNull();
        TaskEligibility.AvailableActions(p3.RequesterId, financeTask, fiona).ShouldBeNull("a decided task offers no buttons");
    }

    [Fact]
    public async Task VisibleTo_ComposesWithTrackingAndIncludes()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var scope = Factory.Services.CreateAsyncScope();
        var (db, rules) = Services(scope);
        var karim = await rules.ContextForAsync(org.Users["karim"], Ct);

        var p1 = await rules.VisibleTo(db.Requests.Include(r => r.Tasks), karim).SingleOrDefaultAsync(r => r.Id == org.Requests["P1"], Ct);
        var hidden = await rules.VisibleTo(db.Requests.Include(r => r.Tasks), await rules.ContextForAsync(org.Users["omar"], Ct))
            .SingleOrDefaultAsync(r => r.Id == org.Requests["P1"], Ct);

        p1.ShouldNotBeNull().Tasks.Count.ShouldBe(1);
        db.Entry(p1).State.ShouldBe(EntityState.Unchanged);
        hidden.ShouldBeNull();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("4294967296")]
    [InlineData("")]
    [InlineData(null)]
    public void VersionToken_RejectsAnythingTheServerNeverIssued(string? token) =>
        VersionToken.TryParse(token, out _).ShouldBeFalse();

    [Fact]
    public void VersionToken_RoundTrips() =>
        (VersionToken.TryParse(VersionToken.Format(4294967295u), out var v) && v == 4294967295u).ShouldBeTrue();

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive), Ct);
    }

    private static (AppDbContext Db, AccessRules Rules) Services(AsyncServiceScope scope) =>
        (scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<AccessRules>());
}
