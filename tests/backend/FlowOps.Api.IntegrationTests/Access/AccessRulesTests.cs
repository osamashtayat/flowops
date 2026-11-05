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
            var actual = TaskEligibility.FailureCode(loaded, loaded.OpenTask!, me);
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

        Should.Throw<ForbiddenException>(() => TaskEligibility.EnsureCanDecide(pf, pf.OpenTask!, finny)).Code.ShouldBe(ErrorCodes.TaskSelfDecision);
        Should.Throw<ForbiddenException>(() => TaskEligibility.EnsureCanDecide(pf, pf.OpenTask!, hana)).Code.ShouldBe(ErrorCodes.TaskNotEligible);
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
            var fromSql = await TaskEligibility.DecidableBy(db.Requests, me).Select(t => t.RequestId).ToListAsync(Ct);
            var inMemory = all.Where(r => TaskEligibility.FailureCode(r, r.OpenTask!, me) is null).Select(r => r.Id);
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
            return TaskEligibility.AvailableActions(loaded, await rules.ContextForAsync(org.Users[user], Ct));
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

    private static (AppDbContext Db, AccessRules Rules) Services(AsyncServiceScope scope) =>
        (scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<AccessRules>());
}
