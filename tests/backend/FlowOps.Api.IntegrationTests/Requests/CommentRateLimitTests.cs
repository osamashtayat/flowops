using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FlowOps.Api.IntegrationTests.Access;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>
/// Per-user limit on <c>POST /api/requests/{id}/comments</c> (#37). Each test runs its own app instance so the in-memory
/// limiter starts empty; the limiter uses real time, so window expiry isn't tested here.
/// </summary>
public sealed class CommentRateLimitTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    [Fact]
    public async Task Comment_OverTheLimit_Returns429WithRetryAfterAndSavesNothing()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var app = WithCommentLimit(2);
        using var lina = await ClientForAsync(app, "lina");
        for (var i = 0; i < 2; i++)
        {
            using var allowed = await lina.PostAsJsonAsync(CommentsUri(org), new { body = $"note {i}" }, Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var limited = await lina.PostAsJsonAsync(CommentsUri(org), new { body = "one too many" }, Ct);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.RequestCommentRateLimited);
        limited.Headers.RetryAfter.ShouldNotBeNull();
        var activity = await JsonBody.ReadAsync(await lina.GetAsync(new Uri($"/api/requests/{org.Requests["L1"]}/activity", UriKind.Relative), Ct));
        activity.EnumerateArray().Count(a => a.GetProperty("type").GetString() == "CommentAdded").ShouldBe(2);
    }

    [Fact]
    public async Task Comment_LimitIsPerUser()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var app = WithCommentLimit(1);
        using var lina = await ClientForAsync(app, "lina");
        using var karim = await ClientForAsync(app, "karim");
        (await lina.PostAsJsonAsync(CommentsUri(org), new { body = "first" }, Ct)).Dispose();

        using var limited = await lina.PostAsJsonAsync(CommentsUri(org), new { body = "second" }, Ct);
        using var other = await karim.PostAsJsonAsync(CommentsUri(org), new { body = "manager note" }, Ct);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.RequestCommentRateLimited);
        other.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Comment_LimitSpansRequests()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var app = WithCommentLimit(1);
        using var lina = await ClientForAsync(app, "lina");
        (await lina.PostAsJsonAsync(CommentsUri(org), new { body = "on the leave" }, Ct)).Dispose();

        using var limited = await lina.PostAsJsonAsync(new Uri($"/api/requests/{org.Requests["P1"]}/comments", UriKind.Relative), new { body = "on the laptop" }, Ct);

        await ProblemAssert.HasCode(limited, 429, ErrorCodes.RequestCommentRateLimited);
    }

    [Fact]
    public async Task Startup_WithZeroCommentLimit_FailsFast() =>
        Should.Throw<Exception>(() => WithCommentLimit(0).CreateClient().Dispose());

    [Fact]
    public async Task Comment_LimitDoesNotApplyToOtherEndpoints()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var app = WithCommentLimit(1);
        using var lina = await ClientForAsync(app, "lina");
        (await lina.PostAsJsonAsync(CommentsUri(org), new { body = "first" }, Ct)).Dispose();

        for (var i = 0; i < 3; i++)
        {
            using var read = await lina.GetAsync(new Uri($"/api/requests/{org.Requests["L1"]}", UriKind.Relative), Ct);
            read.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Comment_Anonymous_Returns401NotRateLimited()
    {
        var org = await AccessOrg.SeedAsync(Factory);
        await using var app = WithCommentLimit(1);
        using var anonymous = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        for (var i = 0; i < 2; i++)
        {
            using var response = await anonymous.PostAsJsonAsync(CommentsUri(org), new { body = "hi" }, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private static Uri CommentsUri(AccessOrg org) => new($"/api/requests/{org.Requests["L1"]}/comments", UriKind.Relative);

    private WebApplicationFactory<Program> WithCommentLimit(int permits) => Factory.WithWebHostBuilder(builder =>
        builder.UseSetting("FlowOps:CommentRateLimit:PermitLimit", permits.ToString(CultureInfo.InvariantCulture)));

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> app, string user)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var login = await FlowOpsApiFactory.LoginAsync(client, $"{user}@flowops.test", Given.DefaultPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", FlowOpsApiFactory.XsrfTokenFrom(login));
        return client;
    }
}
