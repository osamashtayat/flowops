using System.Net;
using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using static FlowOps.Api.IntegrationTests.Organization.UsersApiTests;

namespace FlowOps.Api.IntegrationTests.Organization;

/// <summary>BE-08 rules: manager cycles (I13), last administrator, inactive departments, password reset.</summary>
public sealed class UserRulesTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string ResetPassword = "Reset-Pa55word!";

    [Fact]
    public async Task Update_SelfAsManager_Returns422Cycle()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina", managerId: lina.Id), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserManagerCycle);
    }

    [Fact]
    public async Task Update_IndirectCycle_Returns422AndChangesNothing()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var lina = await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);
        var omar = await Given.User("omar").ManagedBy(lina).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(karim.Id), Profile("Karim", managerId: omar.Id), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserManagerCycle);
        var stored = await JsonBody.ReadAsync(await admin.GetAsync(ById(karim.Id), Ct));
        stored.GetProperty("manager").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Update_ManagerFromAnotherBranch_Succeeds()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        var lina = await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);
        var omar = await Given.User("omar").ManagedBy(karim).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(omar.Id), Profile("Omar Q", managerId: lina.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonBody.ReadAsync(response);
        body.GetProperty("displayName").GetString().ShouldBe("Omar Q");
        body.GetProperty("manager").GetProperty("id").GetGuid().ShouldBe(lina.Id);
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(Guid.CreateVersion7()), Profile("X"), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Update_DeactivatingTheLastActiveAdmin_Returns422()
    {
        var gone = await Given.User("gone").WithRoles(Roles.Administrator).Inactive().CreateAsync(Factory);
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(ada.Id), Profile("Ada", isActive: false), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserLastAdmin);
        gone.ShouldNotBeNull("an inactive administrator does not count");
    }

    [Fact]
    public async Task Update_DeactivatingAnAdminWhileAnotherIsActive_EndsTheirSessionAndSignIn()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var beaClient = await Factory.ClientFor(bea);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(bea.Id), Profile("Bea", isActive: false), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(response)).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var me = await beaClient.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct);
        await ProblemAssert.HasCode(me, 401, ErrorCodes.AuthUnauthenticated);
        using var fresh = Factory.CreateHttpsClient();
        using var login = await FlowOpsApiFactory.LoginAsync(fresh, bea.Email, bea.Password);
        await ProblemAssert.HasCode(login, 401, ErrorCodes.AuthInvalidCredentials);
    }

    [Fact]
    public async Task Update_WithoutIsActive_Returns400AndKeepsTheUserActive()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), new { displayName = "Lina R", departmentId = (Guid?)null, managerId = (Guid?)null }, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        (await JsonBody.ReadAsync(await admin.GetAsync(ById(lina.Id), Ct))).GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_UnknownManagerOrDepartment_Returns400(bool unknownManager)
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);
        var unknown = Guid.CreateVersion7();

        using var response = await admin.PutAsJsonAsync(ById(lina.Id),
            Profile("Lina", departmentId: unknownManager ? null : unknown, managerId: unknownManager ? unknown : null), Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe([unknownManager ? "managerId" : "departmentId"]);
    }

    [Fact]
    public async Task Update_DeactivateThenReactivateWithinAMinute_StillEndsOldSessions()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var linaClient = await Factory.ClientFor(lina);
        using var admin = await AdminClientAsync(Factory);

        (await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina", isActive: false), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina", isActive: true), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));

        using var me = await linaClient.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct);
        await ProblemAssert.HasCode(me, 401, ErrorCodes.AuthUnauthenticated);
    }

    [Fact]
    public async Task UpdateRoles_DemotedAdminLosesAdminAccessAtNextRevalidation()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator, Roles.Employee).CreateAsync(Factory);
        using var beaClient = await Factory.ClientFor(bea);
        using var admin = await AdminClientAsync(Factory);

        (await admin.PutAsJsonAsync(ById(bea.Id, "/roles"), RoleSet("Employee"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));

        using var response = await beaClient.GetAsync(new Uri("/api/users", UriKind.Relative), Ct);
        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Theory]
    [InlineData("[99]")]
    [InlineData("[0]")]
    public async Task UpdateRoles_RolesAsNumbers_Returns400(string roles)
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);
        using var content = new StringContent($$"""{"roles":{{roles}}}""", System.Text.Encoding.UTF8, "application/json");

        using var response = await admin.PutAsync(ById(lina.Id, "/roles"), content, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task UpdateRoles_RemovingAdminFromTheLastActiveAdmin_Returns422()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await Factory.ClientFor(ada);

        using var response = await admin.PutAsJsonAsync(ById(ada.Id, "/roles"), RoleSet("Employee"), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.UserLastAdmin);
    }

    [Fact]
    public async Task UpdateRoles_ReplacesTheRoleSet()
    {
        var lina = await Given.User("lina").WithRoles(Roles.Employee, Roles.FinanceOfficer).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id, "/roles"),
            RoleSet("HrOfficer", "Employee", "HrOfficer"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(response)).GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .ShouldBe(["Employee", "HrOfficer"]);
    }

    [Fact]
    public async Task UpdateRoles_RemovingAdminWhileAnotherIsActive_Succeeds()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(bea.Id, "/roles"), RoleSet(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(response)).GetProperty("roles").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task UpdateRoles_UnknownRole_Returns400()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id, "/roles"), RoleSet("Manager"), Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task UpdateRoles_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PutAsJsonAsync(ById(Guid.CreateVersion7(), "/roles"), RoleSet("Employee"), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Update_MovingIntoAnInactiveDepartment_Returns422()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);
        var old = await InactiveDepartmentAsync(admin);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina", departmentId: old), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.DepartmentInactive);
    }

    [Fact]
    public async Task Update_StayingInADepartmentThatBecameInactive_Succeeds()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var lina = await Given.User("lina").InDepartment(sales).CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);
        (await admin.PutAsJsonAsync(new Uri($"/api/departments/{sales.Id}", UriKind.Relative),
            new { name = "Sales", code = "SALES", isActive = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await admin.PutAsJsonAsync(ById(lina.Id), Profile("Lina R", departmentId: sales.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_SetsNewPasswordRequiresChangeAndEndsSessions()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var linaClient = await Factory.ClientFor(lina);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(ById(lina.Id, "/reset-password"), new { newPassword = ResetPassword }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using (var me = await linaClient.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct))
        {
            await ProblemAssert.HasCode(me, 401, ErrorCodes.AuthUnauthenticated);
        }

        using var fresh = Factory.CreateHttpsClient();
        using (var oldPassword = await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, lina.Password))
        {
            await ProblemAssert.HasCode(oldPassword, 401, ErrorCodes.AuthInvalidCredentials);
        }

        using var newPassword = await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, ResetPassword);
        newPassword.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonBody.ReadAsync(newPassword)).GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ResetPassword_ClearsLockout()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var fresh = Factory.CreateHttpsClient();
        for (var i = 0; i < 5; i++)
        {
            (await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, "Wrong-password-123")).Dispose();
        }

        using var admin = await AdminClientAsync(Factory);

        (await admin.PostAsJsonAsync(ById(lina.Id, "/reset-password"), new { newPassword = ResetPassword }, Ct)).Dispose();

        using var login = await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, ResetPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns400OnNewPassword()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(ById(lina.Id, "/reset-password"), new { newPassword = "alllowercase-only" }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["newPassword"]);
    }

    [Fact]
    public async Task ResetPassword_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(ById(Guid.CreateVersion7(), "/reset-password"), new { newPassword = ResetPassword }, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    private static object RoleSet(params string[] roles) => new { roles };

    private static object Profile(string displayName, Guid? departmentId = null, Guid? managerId = null, bool isActive = true) =>
        new { displayName, departmentId, managerId, isActive };
}
