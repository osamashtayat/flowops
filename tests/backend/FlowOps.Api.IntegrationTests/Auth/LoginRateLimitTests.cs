using System.Net;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowOps.Api.IntegrationTests.Auth;

/// <summary>
/// Login rate limits (AUTHORIZATION.md §1.6). Each test runs its own app instance on the shared database, so its
/// in-memory limiters start empty. The limiters use real time, so window expiry is not tested here.
/// </summary>
public sealed class LoginRateLimitTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string WrongPassword = "Wrong-password-123";

    [Fact]
    public async Task Login_OverThePerEmailLimit_Returns429WithRetryAfter()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        await using var app = WithLimits(perIp: 100, perEmail: 3);
        using var client = HttpsClient(app);
        for (var i = 0; i < 3; i++)
        {
            using var allowed = await FlowOpsApiFactory.LoginAsync(client, lina.Email, WrongPassword);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var limited = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.AuthRateLimited);
        limited.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Login_PerEmailLimit_IgnoresCaseAndSurroundingSpaces()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        await using var app = WithLimits(perIp: 100, perEmail: 2);
        using var client = HttpsClient(app);
        using (var first = await FlowOpsApiFactory.LoginAsync(client, lina.Email.ToUpperInvariant(), WrongPassword))
        using (var second = await FlowOpsApiFactory.LoginAsync(client, " " + lina.Email + " ", WrongPassword))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var limited = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.AuthRateLimited);
    }

    [Fact]
    public async Task Login_PerEmailLimit_TreatsUnknownEmailsLikeRealOnes()
    {
        await using var app = WithLimits(perIp: 100, perEmail: 2);
        using var client = HttpsClient(app);
        for (var i = 0; i < 2; i++)
        {
            using var allowed = await FlowOpsApiFactory.LoginAsync(client, "nobody@flowops.test", WrongPassword);
            await ProblemAssert.HasCode(allowed, 401, ErrorCodes.AuthInvalidCredentials);
        }

        using var limited = await FlowOpsApiFactory.LoginAsync(client, "nobody@flowops.test", WrongPassword);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.AuthRateLimited);
    }

    [Fact]
    public async Task Login_PerEmailLimit_DoesNotAffectOtherEmails()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        await using var app = WithLimits(perIp: 100, perEmail: 1);
        using var client = HttpsClient(app);
        using (var first = await FlowOpsApiFactory.LoginAsync(client, "nobody@flowops.test", WrongPassword))
        using (var limited = await FlowOpsApiFactory.LoginAsync(client, "nobody@flowops.test", WrongPassword))
        {
            await ProblemAssert.HasCode(limited, 429, ErrorCodes.AuthRateLimited);
        }

        using var other = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_OverThePerIpLimit_Returns429AcrossEmails()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        await using var app = WithLimits(perIp: 3, perEmail: 100);
        using var client = HttpsClient(app);
        for (var i = 0; i < 3; i++)
        {
            using var allowed = await FlowOpsApiFactory.LoginAsync(client, $"user{i}@flowops.test", WrongPassword);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var limited = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.AuthRateLimited);
        limited.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task PerIpLimit_AppliesOnlyToLogin()
    {
        var lina = await Given.User("lina").CreateAsync(Factory);
        await using var app = WithLimits(perIp: 1, perEmail: 100);
        using var client = HttpsClient(app);
        using (var login = await FlowOpsApiFactory.LoginAsync(client, lina.Email, lina.Password))
        {
            login.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        for (var i = 0; i < 3; i++)
        {
            using var me = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct);
            me.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private WebApplicationFactory<Program> WithLimits(int perIp, int perEmail) => Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("FlowOps:LoginRateLimit:PerIpPermitLimit", perIp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("FlowOps:LoginRateLimit:PerEmailPermitLimit", perEmail.ToString(System.Globalization.CultureInfo.InvariantCulture));
    });

    private static HttpClient HttpsClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
