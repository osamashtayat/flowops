using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Organization;

/// <summary>
/// #76: every admin write re-checks in the database that the caller is still an active Administrator. The session's
/// claims stay valid until the next re-validation (up to a minute), so these tests never advance the clock.
/// </summary>
public sealed class AdminActorRecheckTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    public enum Removal { Demoted, Deactivated }

    private static readonly string[] AdminOnly = ["Administrator"];
    private static readonly string[] EmployeeOnly = ["Employee"];
    private static readonly string[] EmployeeAndFinance = ["Employee", "FinanceOfficer"];

    public static TheoryData<string, Removal> Writes()
    {
        var data = new TheoryData<string, Removal>();
        foreach (var write in new[] { "create-user", "update-user", "roles", "reset-password", "create-department", "update-department" })
        {
            data.Add(write, Removal.Demoted);
            data.Add(write, Removal.Deactivated);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task AdminWrite_ByAdminRemovedInsideTheRevalidationWindow_Returns403AndChangesNothing(string write, Removal removal)
    {
        // Ada stays an active admin, so neither removal trips the last-administrator rule.
        var ada = await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);
        var bea = await Given.User("bea").WithRoles(Roles.Administrator, Roles.Employee).CreateAsync(Factory);
        var lina = await Given.User("lina").CreateAsync(Factory);
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        using var client = await Factory.ClientFor(bea);
        using var adaClient = await Factory.ClientFor(ada);
        await RemoveAdminAsync(adaClient, bea.Id, removal);

        using var response = await SendAsync(client, write, lina.Id, sales.Id);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
        await AssertUnchangedAsync(lina.Id, sales.Id);
    }

    [Fact]
    public async Task AdminWrite_ByActiveAdmin_StillSucceeds()
    {
        var bea = await Given.User("bea").WithRoles(Roles.Administrator).CreateAsync(Factory);
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(bea);

        using var response = await SendAsync(client, "roles", lina.Id, Guid.Empty);

        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string write, Guid linaId, Guid salesId) => write switch
    {
        "create-user" => client.PostAsJsonAsync(Uri("/api/users"), new
        {
            email = "eve@flowops.test", displayName = "Eve", initialPassword = UsersApiTests.InitialPassword,
            departmentId = (Guid?)null, managerId = (Guid?)null, roles = AdminOnly,
        }, Ct),
        "update-user" => client.PutAsJsonAsync(Uri($"/api/users/{linaId}"),
            new { displayName = "Lina", departmentId = (Guid?)null, managerId = (Guid?)null, isActive = false }, Ct),
        "roles" => client.PutAsJsonAsync(Uri($"/api/users/{linaId}/roles"), new { roles = EmployeeAndFinance }, Ct),
        "reset-password" => client.PostAsJsonAsync(Uri($"/api/users/{linaId}/reset-password"), new { newPassword = "Taken-0ver-Pa55word" }, Ct),
        "create-department" => client.PostAsJsonAsync(Uri("/api/departments"), new { name = "Ops", code = "OPS" }, Ct),
        "update-department" => client.PutAsJsonAsync(Uri($"/api/departments/{salesId}"), new { name = "Sales", code = "SALES", isActive = false }, Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(write)),
    };

    /// <summary>Ada removes Bea's admin powers through the API. Bea's session isn't re-validated yet (no clock advance).</summary>
    private static async Task RemoveAdminAsync(HttpClient ada, Guid beaId, Removal removal)
    {
        using var response = removal == Removal.Demoted
            ? await ada.PutAsJsonAsync(Uri($"/api/users/{beaId}/roles"), new { roles = EmployeeOnly }, Ct)
            : await ada.PutAsJsonAsync(Uri($"/api/users/{beaId}"),
                new { displayName = "Bea", departmentId = (Guid?)null, managerId = (Guid?)null, isActive = false }, Ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task AssertUnchangedAsync(Guid linaId, Guid salesId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await users.FindByEmailAsync("eve@flowops.test")).ShouldBeNull();
        var lina = await users.FindByIdAsync(linaId.ToString());
        lina.ShouldNotBeNull();
        lina.IsActive.ShouldBeTrue();
        lina.MustChangePassword.ShouldBeFalse();
        (await users.GetRolesAsync(lina)).ShouldBe([Roles.Employee]);
        (await db.Departments.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
        (await db.Departments.AsNoTracking().SingleAsync(d => d.Id == salesId, Ct)).IsActive.ShouldBeTrue();
    }

    private static Uri Uri(string path) => new(path, UriKind.Relative);
}
