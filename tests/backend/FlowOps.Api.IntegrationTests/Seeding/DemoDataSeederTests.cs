using System.Net;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Application.Access;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Persistence;
using FlowOps.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Seeding;

/// <summary>BE-16: <c>--seed-demo</c> creates the demo company once, through the domain, and only in Development.</summary>
public sealed class DemoDataSeederTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string DemoPassword = "FlowOps-Demo-2026!";

    [Fact]
    public async Task Seed_InDevelopment_CreatesCompanyWithEveryRequestState()
    {
        (await SeedAsync(Factory.Services)).ShouldBeTrue();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Departments.CountAsync(Ct)).ShouldBe(3);
        (await db.Users.CountAsync(Ct)).ShouldBeInRange(10, 12);
        var states = await db.Requests.Select(r => new { r.Type, r.Status, r.StepKey }).ToListAsync(Ct);
        foreach (var type in Enum.GetValues<RequestType>())
        {
            states.Where(s => s.Type == type).Select(s => s.Status).Distinct()
                .ShouldBe(Enum.GetValues<RequestStatus>().Where(s => type == RequestType.EquipmentPurchase || s != RequestStatus.Approved),
                    ignoreOrder: true, customMessage: $"{type} should cover every status it can reach");
        }
    }

    [Fact]
    public async Task Seed_CreatesBothKindsOfStalledTaskAndAPendingRoleChange()
    {
        await SeedAsync(Factory.Services);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stalled = await TaskEligibility.Stalled(db).Select(x => x.Task.AssigneeRole).ToListAsync(Ct);
        stalled.ShouldContain(role => role == null, "a task assigned to an inactive manager");
        stalled.ShouldContain(role => role == Roles.FinanceOfficer, "a finance task whose only officer is the requester");
        var changes = await db.RoleChanges.ToListAsync(Ct);
        changes.ShouldHaveSingleItem().Status.ShouldBe(RoleChangeStatus.Pending);
        (await db.Notifications.AnyAsync(n => n.Type == Domain.Notifications.NotificationType.RoleChangePending, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Seed_UsersSignInWithTheDocumentedPassword_InactiveOnesCannot()
    {
        await SeedAsync(Factory.Services);

        using var client = Factory.CreateHttpsClient();
        using (var ok = await FlowOpsApiFactory.LoginAsync(client, "lina.dev@demo.flowops.test", DemoPassword))
        {
            ok.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var me = JsonDocument.Parse(await ok.Content.ReadAsStringAsync(Ct));
            me.RootElement.GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
        }

        using var inactive = await FlowOpsApiFactory.LoginAsync(Factory.CreateHttpsClient(), "rami.former@demo.flowops.test", DemoPassword);
        inactive.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Seed_Twice_SeedsOnce()
    {
        (await SeedAsync(Factory.Services)).ShouldBeTrue();
        var counts = await CountsAsync();

        (await SeedAsync(Factory.Services)).ShouldBeFalse();

        (await CountsAsync()).ShouldBe(counts);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Seed_OutsideDevelopment_RefusesAndWritesNothing(string environment)
    {
        await using var app = Factory.WithWebHostBuilder(b => b.UseEnvironment(environment));

        var error = await Should.ThrowAsync<InvalidOperationException>(() => SeedAsync(app.Services));

        error.Message.ShouldContain("Development");
        (await CountsAsync()).ShouldBe((0, 0, 0));
    }

    private static async Task<bool> SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(Ct);
    }

    private async Task<(int Users, int Requests, int Notifications)> CountsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.CountAsync(Ct), await db.Requests.CountAsync(Ct), await db.Notifications.CountAsync(Ct));
    }
}
