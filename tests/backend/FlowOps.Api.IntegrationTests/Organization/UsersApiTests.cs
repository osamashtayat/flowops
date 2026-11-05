using System.Net;
using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;

namespace FlowOps.Api.IntegrationTests.Organization;

/// <summary>API_CONTRACT.md §8 users: create, read, list, authorization (BE-08). Business rules: <see cref="UserRulesTests"/>.</summary>
public sealed class UsersApiTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    internal const string InitialPassword = "Initial-Pa55word!";
    private static readonly Uri Users = new("/api/users", UriKind.Relative);

    [Fact]
    public async Task Create_Returns201WithLocationAndMustChangePassword()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test", sales.Id, karim.Id, "Employee", "HrOfficer", "Employee"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonBody.ReadAsync(response);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/users/{body.GetProperty("id").GetGuid()}");
        body.GetProperty("email").GetString().ShouldBe("nour@flowops.test");
        body.GetProperty("displayName").GetString().ShouldBe("Nour");
        body.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        body.GetProperty("department").GetProperty("id").GetGuid().ShouldBe(sales.Id);
        body.GetProperty("manager").GetProperty("id").GetGuid().ShouldBe(karim.Id);
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["Employee", "HrOfficer"]);
        body.GetProperty("directReportCount").GetInt32().ShouldBe(0);
        body.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Create_NewUserCanSignInButMustChangePasswordFirst()
    {
        using var admin = await AdminClientAsync(Factory);
        (await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        using var nour = await Factory.ClientFor(new TestUser(Guid.Empty, "nour@flowops.test", InitialPassword, "Nour"));

        using var response = await nour.GetAsync(new Uri("/api/dashboard", UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthPasswordChangeRequired);
    }

    [Fact]
    public async Task Create_DuplicateEmailInOtherCase_Returns409()
    {
        await Given.User("lina").CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("LINA@flowops.test"), Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.UserDuplicateEmail);
    }

    [Fact]
    public async Task Create_WeakPassword_Returns400OnInitialPassword()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test") with { initialPassword = "alllowercase-only" }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["initialPassword"]);
    }

    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("not-an-email") with { displayName = "" }, Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["email", "displayName"], ignoreOrder: true);
    }

    [Fact]
    public async Task Create_EmailIdentityRejectsAsUserName_Returns400OnEmail()
    {
        using var admin = await AdminClientAsync(Factory);

        // [EmailAddress] accepts it; Identity's user-name rules don't, and such an account could never change its password.
        using var response = await admin.PostAsJsonAsync(Users, NewUser("o'neil@flowops.test"), Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["email"]);
    }

    [Fact]
    public async Task Create_ThenFirstPasswordChange_Succeeds()
    {
        using var admin = await AdminClientAsync(Factory);
        (await admin.PostAsJsonAsync(Users, NewUser("nour.q+ops@flowops.test"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        using var nour = await Factory.ClientFor(new TestUser(Guid.Empty, "nour.q+ops@flowops.test", InitialPassword, "Nour"));

        using var change = await nour.PostAsJsonAsync(new Uri("/api/auth/change-password", UriKind.Relative),
            new { currentPassword = InitialPassword, newPassword = "Brand-new-Pa55word" }, Ct);

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("[99]")]
    [InlineData("[0]")]
    public async Task Create_RolesAsNumbers_Returns400(string roles)
    {
        using var admin = await AdminClientAsync(Factory);
        using var content = new StringContent(
            $$"""{"email":"nour@flowops.test","displayName":"Nour","initialPassword":"{{InitialPassword}}","departmentId":null,"managerId":null,"roles":{{roles}}}""",
            System.Text.Encoding.UTF8, "application/json");

        using var response = await admin.PostAsync(Users, content, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task Create_UnknownManager_Returns400()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test", managerId: Guid.CreateVersion7()), Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["managerId"]);
    }

    [Fact]
    public async Task Create_UnknownDepartment_Returns400()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test", departmentId: Guid.CreateVersion7()), Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["departmentId"]);
    }

    [Fact]
    public async Task Create_InactiveManager_Returns400()
    {
        var gone = await Given.User("gone").Inactive().CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test", managerId: gone.Id), Ct);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["managerId"]);
    }

    [Fact]
    public async Task Create_InInactiveDepartment_Returns422()
    {
        using var admin = await AdminClientAsync(Factory);
        var old = await InactiveDepartmentAsync(admin);

        using var response = await admin.PostAsJsonAsync(Users, NewUser("nour@flowops.test", old), Ct);

        await ProblemAssert.HasCode(response, 422, ErrorCodes.DepartmentInactive);
    }

    [Fact]
    public async Task Get_ReturnsActiveDirectReportCount()
    {
        var karim = await Given.User("karim").CreateAsync(Factory);
        await Given.User("lina").ManagedBy(karim).CreateAsync(Factory);
        await Given.User("omar").ManagedBy(karim).CreateAsync(Factory);
        await Given.User("gone").ManagedBy(karim).Inactive().CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        var body = await JsonBody.ReadAsync(await admin.GetAsync(ById(karim.Id), Ct));

        body.GetProperty("directReportCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.GetAsync(ById(Guid.CreateVersion7()), Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task List_PagesSortedByNameAndHidesInactiveByDefault()
    {
        foreach (var name in new[] { "carla", "bilal", "dina", "emad" })
        {
            await Given.User(name).CreateAsync(Factory);
        }

        await Given.User("aaron").Inactive().CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        var page = await JsonBody.ReadAsync(await admin.GetAsync(new Uri("/api/users?page=2&pageSize=2", UriKind.Relative), Ct));

        // ada (the admin) + 4 active users; aaron is inactive.
        page.GetProperty("totalCount").GetInt32().ShouldBe(5);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        Names(page).ShouldBe(["Carla", "Dina"]);
    }

    [Fact]
    public async Task List_FiltersBySearchDepartmentRoleAndInactive()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        await Given.User("lina").InDepartment(sales).CreateAsync(Factory);
        await Given.User("linus").WithRoles(Roles.FinanceOfficer).CreateAsync(Factory);
        await Given.User("omar").InDepartment(sales).Inactive().CreateAsync(Factory);
        using var admin = await AdminClientAsync(Factory);

        Names(await ListAsync(admin, "search=LIN")).ShouldBe(["Lina", "Linus"]);
        Names(await ListAsync(admin, "search=linus%40flowops")).ShouldBe(["Linus"]);
        Names(await ListAsync(admin, $"departmentId={sales.Id}")).ShouldBe(["Lina"]);
        Names(await ListAsync(admin, $"departmentId={sales.Id}&includeInactive=true")).ShouldBe(["Lina", "Omar"]);
        Names(await ListAsync(admin, "role=FinanceOfficer")).ShouldBe(["Linus"]);
        Names(await ListAsync(admin, "search=%25")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task List_InvalidPaging_Returns400(string query)
    {
        using var admin = await AdminClientAsync(Factory);

        using var response = await admin.GetAsync(new Uri("/api/users?" + query, UriKind.Relative), Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/{id}")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/{id}")]
    [InlineData("PUT", "/{id}/roles")]
    [InlineData("POST", "/{id}/reset-password")]
    public async Task UserEndpoints_AsNonAdmin_Return403(string method, string path)
    {
        var lina = await Given.User("lina").WithRoles(Roles.Employee, Roles.HrOfficer, Roles.FinanceOfficer).CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/users" + path.Replace("{id}", lina.Id.ToString(), StringComparison.Ordinal))
        {
            Content = method == "GET" ? null : JsonContent.Create(new { }),
        };

        using var response = await client.SendAsync(request, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    internal static async Task<HttpClient> AdminClientAsync(FlowOpsApiFactory factory)
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(factory);
        return await factory.ClientFor(ada);
    }

    /// <summary>Creates an inactive department through the admin API.</summary>
    internal static async Task<Guid> InactiveDepartmentAsync(HttpClient admin)
    {
        ArgumentNullException.ThrowIfNull(admin);
        using var created = await admin.PostAsJsonAsync(new Uri("/api/departments", UriKind.Relative), new { name = "Old", code = "OLD" }, Ct);
        var id = (await JsonBody.ReadAsync(created)).GetProperty("id").GetGuid();
        using var deactivated = await admin.PutAsJsonAsync(new Uri($"/api/departments/{id}", UriKind.Relative),
            new { name = "Old", code = "OLD", isActive = false }, Ct);
        deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        return id;
    }

    internal static Uri ById(Guid id, string suffix = "") => new($"/api/users/{id}{suffix}", UriKind.Relative);

    private static NewUserBody NewUser(string email, Guid? departmentId = null, Guid? managerId = null, params string[] roles) =>
        new(email, "Nour", InitialPassword, departmentId, managerId, roles.Length == 0 ? ["Employee"] : roles);

    private static async Task<System.Text.Json.JsonElement> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri("/api/users?" + query, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonBody.ReadAsync(response);
    }

    private static List<string?> Names(System.Text.Json.JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("displayName").GetString()).Where(n => n != "Ada")];

    private sealed record NewUserBody(string email, string displayName, string initialPassword, Guid? departmentId, Guid? managerId, string[] roles);
}
