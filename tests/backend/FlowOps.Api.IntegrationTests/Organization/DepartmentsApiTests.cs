using System.Net;
using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;

namespace FlowOps.Api.IntegrationTests.Organization;

/// <summary>API_CONTRACT.md §8 departments (BE-08).</summary>
public sealed class DepartmentsApiTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly Uri Departments = new("/api/departments", UriKind.Relative);

    [Fact]
    public async Task Create_AsAdmin_Returns201WithNormalizedCode()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Departments, new { name = " Sales ", code = "sales-1" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var body = await JsonBody.ReadAsync(response);
        body.GetProperty("name").GetString().ShouldBe("Sales");
        body.GetProperty("code").GetString().ShouldBe("SALES-1");
        body.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        body.GetProperty("memberCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Create_DuplicateCodeInOtherCase_Returns409()
    {
        await Given.DepartmentAsync(Factory, "Sales", "SALES");
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Departments, new { name = "Field Sales", code = "sales" }, Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.DepartmentDuplicate);
    }

    [Fact]
    public async Task Create_DuplicateNameInOtherCase_Returns409()
    {
        await Given.DepartmentAsync(Factory, "Sales", "SALES");
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Departments, new { name = "SALES", code = "S2" }, Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.DepartmentDuplicate);
    }

    [Theory]
    [InlineData("", "SALES")]
    [InlineData("Sales", "SA LES")]
    [InlineData("Sales", "ABCDEFGHIJKLMNOPQ")]
    public async Task Create_InvalidInput_Returns400(string name, string code)
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Departments, new { name, code }, Ct);

        await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
    }

    [Fact]
    public async Task Create_AsEmployee_Returns403()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.PostAsJsonAsync(Departments, new { name = "Sales", code = "SALES" }, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task Update_RenamesAndDeactivates()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync(ById(sales.Id), new { name = "Field Sales", code = "fs", isActive = false }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonBody.ReadAsync(response);
        body.GetProperty("name").GetString().ShouldBe("Field Sales");
        body.GetProperty("code").GetString().ShouldBe("FS");
        body.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Update_ToAnotherDepartmentsCode_Returns409()
    {
        await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var hr = await Given.DepartmentAsync(Factory, "HR", "HR");
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync(ById(hr.Id), new { name = "HR", code = "SALES", isActive = true }, Ct);

        await ProblemAssert.HasCode(response, 409, ErrorCodes.DepartmentDuplicate);
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync(ById(Guid.CreateVersion7()), new { name = "X", code = "X", isActive = true }, Ct);

        await ProblemAssert.HasCode(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Update_AsEmployee_Returns403()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.PutAsJsonAsync(ById(sales.Id), new { name = "X", code = "X", isActive = true }, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task List_AsEmployee_ReturnsActiveOnlyEvenWhenAskingForInactive()
    {
        await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var old = await Given.DepartmentAsync(Factory, "Old", "OLD");
        using var admin = await AdminClientAsync();
        (await admin.PutAsJsonAsync(ById(old.Id), new { name = "Old", code = "OLD", isActive = false }, Ct)).Dispose();
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        var names = await NamesAsync(client, "/api/departments?includeInactive=true");

        names.ShouldBe(["Sales"]);
    }

    [Fact]
    public async Task List_AsAdminWithInactive_ReturnsAllSortedWithActiveMemberCounts()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var old = await Given.DepartmentAsync(Factory, "Archive", "ARCH");
        await Given.User("lina").InDepartment(sales).CreateAsync(Factory);
        await Given.User("omar").InDepartment(sales).CreateAsync(Factory);
        await Given.User("gone").InDepartment(sales).Inactive().CreateAsync(Factory);
        using var admin = await AdminClientAsync();
        (await admin.PutAsJsonAsync(ById(old.Id), new { name = "Archive", code = "ARCH", isActive = false }, Ct)).Dispose();

        using var response = await admin.GetAsync(new Uri("/api/departments?includeInactive=true", UriKind.Relative), Ct);

        var items = (await JsonBody.ReadAsync(response)).EnumerateArray().ToList();
        items.Select(d => d.GetProperty("name").GetString()).ShouldBe(["Archive", "Sales"]);
        items[1].GetProperty("memberCount").GetInt32().ShouldBe(2);
        (await NamesAsync(admin, "/api/departments")).ShouldBe(["Sales"]);
    }

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await client.GetAsync(Departments, Ct);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        return await Factory.ClientFor(ada);
    }

    private static Uri ById(Guid id) => new($"/api/departments/{id}", UriKind.Relative);

    private static async Task<List<string?>> NamesAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. (await JsonBody.ReadAsync(response)).EnumerateArray().Select(d => d.GetProperty("name").GetString())];
    }
}
