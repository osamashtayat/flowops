using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;

namespace FlowOps.Api.IntegrationTests.Auth;

/// <summary>POST /api/auth/change-password and the must-change-password gate (AUTHORIZATION.md §1.6).</summary>
public sealed class PasswordChangeTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string NewPassword = "Brand-new-Pa55word";
    private static readonly Uri ChangePassword = new("/api/auth/change-password", UriKind.Relative);
    private static readonly Uri Me = new("/api/auth/me", UriKind.Relative);
    private static readonly Uri Dashboard = new("/api/dashboard", UriKind.Relative);

    [Fact]
    public async Task MustChangePasswordUser_IsBlockedFromNonAuthEndpoints()
    {
        var lina = await Given.User("lina").MustChangePassword().CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.GetAsync(Dashboard, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthPasswordChangeRequired);
    }

    [Fact]
    public async Task MustChangePasswordUser_CanStillCallMe()
    {
        var lina = await Given.User("lina").MustChangePassword().CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.GetAsync(Me, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        json.RootElement.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ChangePassword_LiftsTheGateImmediately()
    {
        var lina = await Given.User("lina").MustChangePassword().CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var change = await PostChangeAsync(client, lina.Password, NewPassword);

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var dashboard = await client.GetAsync(Dashboard, Ct);
        dashboard.StatusCode.ShouldBe(HttpStatusCode.NotImplemented, "past the gate: the dashboard is still a BE-15 stub");
        using var json = JsonDocument.Parse(await client.GetStringAsync(Me, Ct));
        json.RootElement.GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task ChangePassword_NewPasswordSignsInAndOldDoesNot()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        using var change = await PostChangeAsync(client, lina.Password, NewPassword);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var fresh = Factory.CreateHttpsClient();
        using var oldLogin = await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, lina.Password);
        await ProblemAssert.HasCode(oldLogin, 401, ErrorCodes.AuthInvalidCredentials);
        using var newLogin = await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, NewPassword);
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400OnCurrentPassword()
    {
        var lina = await Given.User("lina").MustChangePassword().CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await PostChangeAsync(client, "Not-the-Pa55word", NewPassword);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["currentPassword"]);
        using var dashboard = await client.GetAsync(Dashboard, Ct);
        await ProblemAssert.HasCode(dashboard, 403, ErrorCodes.AuthPasswordChangeRequired);
    }

    [Fact]
    public async Task ChangePassword_BreakingIdentityRules_Returns400OnNewPassword()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await PostChangeAsync(client, lina.Password, "alllowercaseletters");

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["newPassword"]);
    }

    [Fact]
    public async Task ChangePassword_ShorterThanTwelve_Returns400OnNewPassword()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await PostChangeAsync(client, lina.Password, "Sh0rt-pw!");

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["newPassword"]);
    }

    [Fact]
    public async Task ChangePassword_ToTheSamePassword_Returns400OnNewPassword()
    {
        var lina = await Given.User("lina").MustChangePassword().CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await PostChangeAsync(client, lina.Password, lina.Password);

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["newPassword"]);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPasswordFiveTimes_LocksTheAccount()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);
        for (var i = 0; i < 5; i++)
        {
            using var failed = await PostChangeAsync(client, "Not-the-Pa55word", NewPassword);
            await ProblemAssert.HasCode(failed, 400, ErrorCodes.Validation);
        }

        using var fresh = Factory.CreateHttpsClient();
        using var login = await FlowOpsApiFactory.LoginAsync(fresh, lina.Email, lina.Password);

        // Locked out answers exactly like a wrong password (AUTHORIZATION.md §1.6).
        await ProblemAssert.HasCode(login, 401, ErrorCodes.AuthInvalidCredentials);
    }

    [Fact]
    public async Task ChangePassword_EndsTheUsersOtherSessionsAtRevalidation()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var laptop = await Factory.ClientFor(lina);
        using var phone = await Factory.ClientFor(lina);

        using var change = await PostChangeAsync(laptop, lina.Password, NewPassword);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.Clock.Advance(TimeSpan.FromMinutes(2));

        (await laptop.GetAsync(Me, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var stale = await phone.GetAsync(Me, Ct);
        await ProblemAssert.HasCode(stale, 401, ErrorCodes.AuthUnauthenticated);
    }

    [Fact]
    public async Task MustChangePasswordUser_IsBlockedFromUnsafeNonAuthEndpoints()
    {
        var lina = await Given.User("lina").MustChangePassword().CreateAsync(Factory);
        using var client = await Factory.ClientFor(lina);

        using var response = await client.PostAsJsonAsync(new Uri("/api/notifications/read-all", UriKind.Relative), new { }, Ct);

        await ProblemAssert.HasCode(response, 403, ErrorCodes.AuthPasswordChangeRequired);
    }

    [Fact]
    public async Task ChangePassword_Anonymous_Returns401()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await PostChangeAsync(client, "whatever-password", NewPassword);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    private static Task<HttpResponseMessage> PostChangeAsync(HttpClient client, string current, string next) =>
        client.PostAsJsonAsync(ChangePassword, new { currentPassword = current, newPassword = next }, Ct);
}
