using System.Net;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using FlowOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Auth;

public sealed class LoginTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_Returns200CurrentUser()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var body = json.RootElement;
        body.GetProperty("id").GetGuid().ShouldBe(lina.Id);
        body.GetProperty("email").GetString().ShouldBe(lina.Email);
        body.GetProperty("displayName").GetString().ShouldBe("Lina");
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["Employee"]);
        body.GetProperty("capabilities").EnumerateArray().Select(r => r.GetString()).ShouldBe(["requests.submit"]);
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Login_SetsHostPrefixedHttpOnlyStrictSessionCookie()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        var session = SetCookie(response, "__Host-flowops=");
        session.ShouldContain("secure", Case.Insensitive);
        session.ShouldContain("httponly", Case.Insensitive);
        session.ShouldContain("samesite=strict", Case.Insensitive);
        session.ShouldContain("path=/", Case.Insensitive);
        session.ShouldNotContain("domain=", Case.Insensitive);
        session.ShouldNotContain("expires=", Case.Insensitive);
    }

    [Fact]
    public async Task Login_IssuesReadableXsrfCookie()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        var xsrf = SetCookie(response, "XSRF-TOKEN=");
        xsrf.ShouldNotContain("httponly", Case.Insensitive);
        xsrf.ShouldContain("secure", Case.Insensitive);
        xsrf.ShouldContain("samesite=strict", Case.Insensitive);
        FlowOpsApiFactory.XsrfTokenFrom(response).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401InvalidCredentials()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, lina.Email, "Wrong-password-123");

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthInvalidCredentials);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401InvalidCredentials()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, "nobody@flowops.test", "Whatever-password-1");

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthInvalidCredentials);
    }

    [Fact]
    public async Task Login_InactiveUser_Returns401InvalidCredentials()
    {
        var gone = await Given.User("gone").Inactive().CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, gone.Email, gone.Password);

        await ProblemAssert.HasCode(response, 401, ErrorCodes.AuthInvalidCredentials);
    }

    [Fact]
    public async Task Login_WithMalformedBody_Returns400Validation()
    {
        using var client = Factory.CreateHttpsClient();

        using var response = await FlowOpsApiFactory.LoginAsync(client, "not-an-email", "");

        var problem = await ProblemAssert.HasCode(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe(["email", "password"], ignoreOrder: true);
    }

    [Fact]
    public async Task Login_AfterFiveFailures_Returns401EvenWithCorrectPassword()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await FlowOpsApiFactory.LoginAsync(client, lina.Email, "Wrong-password-123");
            await ProblemAssert.HasCode(failed, 401, ErrorCodes.AuthInvalidCredentials);
        }

        using var correct = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        await ProblemAssert.HasCode(correct, 401, ErrorCodes.AuthInvalidCredentials);
    }

    [Fact]
    public async Task Login_LockedOutAccount_AnswersExactlyLikeAnUnknownEmail()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await FlowOpsApiFactory.LoginAsync(client, lina.Email, "Wrong-password-123");
        }

        using var locked = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);
        using var unknown = await FlowOpsApiFactory.LoginAsync(client, "nobody@flowops.test", lina.Password);

        var lockedProblem = await ProblemAssert.HasCode(locked, 401, ErrorCodes.AuthInvalidCredentials);
        var unknownProblem = await ProblemAssert.HasCode(unknown, 401, ErrorCodes.AuthInvalidCredentials);
        lockedProblem.GetProperty("detail").GetString().ShouldBe(unknownProblem.GetProperty("detail").GetString());
    }

    // Identity's lockout reads the system clock, not the injected TimeProvider, so this test checks the stored
    // lockout end against real time and then moves that end into the past instead of advancing TestClock.
    [Fact]
    public async Task Lockout_LastsFifteenMinutesThenLoginSucceeds()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        using var client = Factory.CreateHttpsClient();
        var before = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            using var failed = await FlowOpsApiFactory.LoginAsync(client, lina.Email, "Wrong-password-123");
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lockoutEnd = await db.Users.Where(u => u.Id == lina.Id).Select(u => u.LockoutEnd).SingleAsync(Ct);
            lockoutEnd.ShouldNotBeNull();
            lockoutEnd.Value.ShouldBeInRange(before.AddMinutes(15), DateTimeOffset.UtcNow.AddMinutes(15));

            await db.Users.Where(u => u.Id == lina.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddSeconds(-1)), Ct);
        }

        using var response = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string SetCookie(HttpResponseMessage response, string prefix) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(prefix, StringComparison.Ordinal));
}
