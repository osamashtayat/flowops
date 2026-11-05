using System.Net;
using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using static FlowOps.Api.IntegrationTests.Organization.UsersApiTests;

namespace FlowOps.Api.IntegrationTests.Organization;

/// <summary>
/// Admins can't use admin powers to gain process authority (decided 2026-11-06, AUTHORIZATION.md §2): no changing their
/// own roles or manager link, no making themselves anyone's manager. Other admins still can.
/// </summary>
public sealed class SelfAdministrationTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    [Fact]
    public async Task UpdateRoles_OnSelf_Returns422AndKeepsRoles()
    {
        await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Administrator", "FinanceOfficer"), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserSelfAdministration);
        (await JsonBody.ReadAsync(await admin.GetAsync(ById(ada.Id), Ct))).GetProperty("roles").EnumerateArray()
            .Select(r => r.GetString()).ShouldBe(["Administrator"]);
    }

    [Fact]
    public async Task UpdateRoles_OnAnotherAdmin_IsAllowed()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(bea.Id, "/roles"), RoleSet("Administrator", "HrOfficer"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_SettingOwnManager_Returns422()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(ada.Id), Profile("Ada", managerId: karim.Id), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserSelfAdministration);
    }

    [Fact]
    public async Task Update_RemovingOwnManager_Returns422()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).ManagedBy(karim).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(ada.Id), Profile("Ada"), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserSelfAdministration);
    }

    [Fact]
    public async Task Update_OwnDisplayNameWithManagerUnchanged_IsAllowed()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).ManagedBy(karim).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(ada.Id), Profile("Ada Lovelace", managerId: karim.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_MakingSelfSomeonesManager_Returns422AndKeepsTheLink()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var lina = await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator, Roles.Employee).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina", managerId: ada.Id), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserSelfAdministration);
        (await JsonBody.ReadAsync(await admin.GetAsync(ById(lina.Id), Ct))).GetProperty("manager").GetProperty("id").GetGuid().ShouldBe(karim.Id);
    }

    [Fact]
    public async Task Update_KeepingSelfAsExistingManager_IsAllowed()
    {
        // The link predates the rule (or another admin made it); editing other fields doesn't re-check it.
        var ada = await Given.User("ada").WithRoles(Roles.Administrator, Roles.Employee).CreateAsync(Factory);
        var lina = await Given.User("lina").ManagedBy(ada).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina R", managerId: ada.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_AnotherAdminMakesThemAManager_IsAllowed()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator, Roles.Employee).CreateAsync(Factory);
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina", managerId: bea.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithSelfAsManager_Returns422AndCreatesNothing()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator, Roles.Employee).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PostAsJsonAsync(new Uri("/api/users", UriKind.Relative), new
        {
            email = "nour@flowops.test", displayName = "Nour", initialPassword = InitialPassword,
            departmentId = (Guid?)null, managerId = ada.Id, roles = EmployeeOnly,
        }, Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserSelfAdministration);
        var list = await JsonBody.ReadAsync(await admin.GetAsync(new Uri("/api/users?search=nour", UriKind.Relative), Ct));
        list.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    private static readonly string[] EmployeeOnly = ["Employee"];

    private static object RoleSet(params string[] roles) => new { roles };

    private static object Profile(string displayName, Guid? managerId = null) =>
        new { displayName, departmentId = (Guid?)null, managerId, isActive = true };
}
