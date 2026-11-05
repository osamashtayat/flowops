using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Access;

/// <summary>
/// A seeded organisation with requests at every stage, for the AccessRules / TaskEligibility tables (BE-09).
/// Requests are written through the domain and AppDbContext because no submission endpoint exists yet (BE-10).
/// <code>
/// boss ─manages→ karim ─manages→ lina, finny, omar
/// fiona, finny: FinanceOfficer · hana: HrOfficer · pete: ProcurementOfficer · ada: Administrator
/// gone: inactive FinanceOfficer
/// P1 lina purchase @ManagerApproval (karim) · P2 lina purchase @FinanceApproval
/// P3 lina purchase @ProcurementFulfillment (finance decided by fiona) · L1 lina leave @HrConfirmation
/// PF finny's own purchase @FinanceApproval
/// </code>
/// </summary>
internal sealed record AccessOrg(IReadOnlyDictionary<string, Guid> Users, IReadOnlyDictionary<string, Guid> Requests)
{
    public static async Task<AccessOrg> SeedAsync(FlowOpsApiFactory factory)
    {
        var sales = await Given.DepartmentAsync(factory, "Sales", "SALES");
        var boss = await Given.User("boss").InDepartment(sales).CreateAsync(factory);
        var karim = await Given.User("karim").InDepartment(sales).ManagedBy(boss).CreateAsync(factory);
        var users = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["boss"] = boss.Id,
            ["karim"] = karim.Id,
            ["lina"] = (await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(factory)).Id,
            ["finny"] = (await Given.User("finny").InDepartment(sales).ManagedBy(karim).WithRoles(Roles.Employee, Roles.FinanceOfficer).CreateAsync(factory)).Id,
            ["omar"] = (await Given.User("omar").InDepartment(sales).ManagedBy(karim).CreateAsync(factory)).Id,
            ["fiona"] = (await Given.User("fiona").WithRoles(Roles.FinanceOfficer).CreateAsync(factory)).Id,
            ["hana"] = (await Given.User("hana").WithRoles(Roles.HrOfficer).CreateAsync(factory)).Id,
            ["pete"] = (await Given.User("pete").WithRoles(Roles.ProcurementOfficer).CreateAsync(factory)).Id,
            ["ada"] = (await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(factory)).Id,
            ["gone"] = (await Given.User("gone").WithRoles(Roles.FinanceOfficer).Inactive().CreateAsync(factory)).Id,
        };

        var now = factory.Clock.GetUtcNow();
        Assignee Resolve(ProcessStep step) =>
            step.Assignee.IsRequesterManager ? Assignee.ForUser(karim.Id) : Assignee.ForRole(step.Assignee.Role!);
        Request Purchase(long seq, string requester) => Request.SubmitPurchase(RequestNumber.Create(2026, seq), users[requester], sales.Id,
            PurchaseDetails.Create("Laptop", "14 inch", Money.Create(1250.500m, SupportedCurrencies.Jod), "Broken"), karim.Id, now);
        void Approve(Request request, string actor) =>
            request.DecideTask(request.OpenTask!.Id, users[actor], TaskDecision.Approve(null), Resolve, now);

        var p1 = Purchase(1, "lina");
        var p2 = Purchase(2, "lina");
        Approve(p2, "karim");
        var p3 = Purchase(3, "lina");
        Approve(p3, "karim");
        Approve(p3, "fiona");
        var l1 = Request.SubmitLeave(RequestNumber.Create(2026, 4), users["lina"], sales.Id,
            LeaveDetails.Create(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12), LeaveType.Annual, "Family", now, TimeZoneInfo.Utc),
            karim.Id, now);
        Approve(l1, "karim");
        var pf = Purchase(5, "finny");
        Approve(pf, "karim");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Requests.AddRange(p1, p2, p3, l1, pf);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new AccessOrg(users, new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["P1"] = p1.Id, ["P2"] = p2.Id, ["P3"] = p3.Id, ["L1"] = l1.Id, ["PF"] = pf.Id,
        });
    }

    public string RequestName(Guid id) => Requests.Single(r => r.Value == id).Key;
}
