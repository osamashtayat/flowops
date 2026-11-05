using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Auth;

/// <summary>/me, logout, XSRF enforcement, and what ends a session.</summary>
public sealed class SessionTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private static readonly Uri Me = new("/api/auth/me", UriKind.Relative);
    private static readonly Uri Logout = new("/api/auth/logout", UriKind.Relative);

    [Fact]
    public async Task Me_Anonymous_Returns401ProblemNotRedirect()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await client.GetAsync(Me, Ct);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Me_ReturnsDepartmentAndManager()
    {
        var sales = await Given.DepartmentAsync(Factory, "Sales", "SALES");
        var karim = await Given.User("karim").InDepartment(sales).CreateAsync(Factory);
        var lina = await Given.User("lina").InDepartment(sales).ManagedBy(karim).CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.GetAsync(Me, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        json.RootElement.GetProperty("department").GetProperty("id").GetGuid().ShouldBe(sales.Id);
        json.RootElement.GetProperty("department").GetProperty("name").GetString().ShouldBe("Sales");
        json.RootElement.GetProperty("manager").GetProperty("id").GetGuid().ShouldBe(karim.Id);
        json.RootElement.GetProperty("manager").GetProperty("displayName").GetString().ShouldBe("Karim");
    }

    [Fact]
    public async Task Me_WithoutDepartmentOrManager_ReturnsNulls()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var json = JsonDocument.Parse(await client.GetStringAsync(Me, Ct));

        json.RootElement.GetProperty("department").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.GetProperty("manager").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Me_RefreshesXsrfCookie()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.GetAsync(Me, Ct);

        FlowOpsApiFactory.XsrfTokenFrom(response).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnsafeRequest_WithoutXsrfHeader_Returns403()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        using var response = await client.PostAsync(Logout, null, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
        (await client.GetAsync(Me, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK, "the rejected logout must not end the session");
    }

    [Fact]
    public async Task UnsafeRequest_WithForgedXsrfHeader_Returns403()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "forged");

        using var response = await client.PostAsync(Logout, null, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task XsrfToken_OfAnotherUser_IsRejected()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        var omar = await Given.User("omar").CreateAsync(Factory);
        using var linaClient = await Factory.ClientFor(lina);
        using var omarClient = await Factory.ClientFor(omar);
        linaClient.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        linaClient.DefaultRequestHeaders.Add("X-XSRF-TOKEN", omarClient.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single());

        using var response = await linaClient.PostAsync(Logout, null, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var logout = await client.PostAsync(Logout, null, Ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var me = await client.GetAsync(Me, Ct);
        await ProblemAssert.HasCode(me, 401, ErrorCodes.AuthUnauthenticated);
    }

    [Fact]
    public async Task Logout_RevokesACopiedCookieAtNextRevalidation()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();
        using var login = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);
        var sessionCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-flowops=", StringComparison.Ordinal)).Split(';')[0];
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", FlowOpsApiFactory.XsrfTokenFrom(login));
        using var thief = Factory.CreateHttpsClient();
        using (var before = await GetMeWithCookieAsync(thief, sessionCookie))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.OK, "control: the copied cookie works while the session lives");
        }

        using var logout = await client.PostAsync(Logout, null, Ct);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var response = await GetMeWithCookieAsync(thief, sessionCookie);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    private static async Task<HttpResponseMessage> GetMeWithCookieAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Me);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Session_OfDeactivatedUser_EndsAtNextRevalidation()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        await SetActiveAsync(lina, false);

        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var response = await client.GetAsync(Me, Ct);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    [Fact]
    public async Task Session_AfterEightIdleHours_Expires()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        Factory.Clock.Advance(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));
        using var response = await client.GetAsync(Me, Ct);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    [Fact]
    public async Task RolesSeededByMigration_AreExactlyTheDomainRoles()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var names = await db.Roles.Select(r => r.Name).ToListAsync(Ct);

        names.ShouldBe(Roles.All, ignoreOrder: true);
    }

    private async Task SetActiveAsync(TestUser user, bool active)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, active), Ct);
    }
}
