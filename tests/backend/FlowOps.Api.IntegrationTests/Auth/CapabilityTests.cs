using System.Net;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;

namespace FlowOps.Api.IntegrationTests.Auth;

/// <summary>Capabilities per role combination (AUTHORIZATION.md §2), and that policies enforce the same set.</summary>
public sealed class CapabilityTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly Uri Me = new("/api/auth/me", UriKind.Relative);
    private static readonly Uri Tasks = new("/api/tasks", UriKind.Relative);

    public static TheoryData<string[], string[]> RoleCombinations => new()
    {
        { [Roles.Employee], ["requests.submit"] },
        { [Roles.Employee, Roles.FinanceOfficer], ["requests.submit", "tasks.view"] },
        { [Roles.Employee, Roles.HrOfficer], ["requests.submit", "tasks.view"] },
        { [Roles.Employee, Roles.ProcurementOfficer], ["requests.submit", "tasks.view"] },
        { [Roles.Administrator], ["admin.manage", "workflows.manage"] },
        { [Roles.Administrator, Roles.Employee], ["requests.submit", "admin.manage", "workflows.manage"] },
        { [Roles.FinanceOfficer], ["tasks.view"] },
        { [], [] },
    };

    [Theory]
    [MemberData(nameof(RoleCombinations))]
    public async Task Me_ReturnsCapabilitiesForRoles(string[] roles, string[] expected)
    {
        var user = await Given.User("lina").WithRoles(roles).CreateAsync(Factory);

        (await CapabilitiesOf(user)).ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public async Task Me_UserWithActiveDirectReport_HasTasksView()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);

        (await CapabilitiesOf(karim)).ShouldBe(["requests.submit", "tasks.view"], ignoreOrder: true);
    }

    [Fact]
    public async Task Me_UserWhoseOnlyReportIsInactive_IsNotAManager()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        await Given.User("lina").ManagedBy(karim).Inactive().CreateAsync(Factory);

        (await CapabilitiesOf(karim)).ShouldBe(["requests.submit"]);
    }

    [Fact]
    public async Task TasksPolicy_AdmitsDerivedManager()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);
        using var client = await Factory.ClientFor(karim);

        using var response = await client.GetAsync(Tasks, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TasksPolicy_RejectsPlainEmployee()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.GetAsync(Tasks, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task AdminPolicy_RejectsNonAdminAndAdmitsAdmin()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var employee = await Factory.ClientFor(lina);
        using var admin = await Factory.ClientFor(ada);
        var users = new Uri("/api/users", UriKind.Relative);

        using var forbidden = await employee.GetAsync(users, Ct);
        using var allowed = await admin.GetAsync(users, Ct);

        await ProblemAssert.HasCode(forbidden, 403, ErrorCodes.AuthForbidden);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string?[]> CapabilitiesOf(TestUser user)
    {
        using var client = await Factory.ClientFor(user);
        using var json = JsonDocument.Parse(await client.GetStringAsync(Me, Ct));
        return [.. json.RootElement.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString())];
    }
}
