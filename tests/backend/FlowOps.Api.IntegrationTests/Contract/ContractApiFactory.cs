using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Contract;

/// <summary>
/// The real Api with <see cref="TestAuthHandler"/> as the default scheme and antiforgery accepted, so the
/// tests isolate routes, policies and input validation. Needs no database. Real sign-in and XSRF: Auth/AuthTests.
/// </summary>
public sealed class ContractApiFactory : WebApplicationFactory<Program>
{
    public HttpClient ClientWithRoles(params string[] roles)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }

    /// <summary>
    /// Never opened: requests that reach a controller only need the DbContext to be constructible. Set explicitly so the
    /// tests don't depend on a connection string in the developer's user-secrets (absent in CI, OPS-02).
    /// </summary>
    private const string UnusedConnectionString = "Host=contract-tests.invalid;Database=unused";

    // Development loads the developer's user-secrets; a configured bootstrap admin would make start-up open the database.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting("ConnectionStrings:FlowOps", UnusedConnectionString)
            .UseSetting("FlowOps:BootstrapAdmin:Email", "")
            .UseSetting("FlowOps:BootstrapAdmin:Password", "")
            .ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            services.AddSingleton<IAntiforgery, AcceptingAntiforgery>();
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });
        });
}

/// <summary>Treats every request as carrying a valid XSRF token.</summary>
internal sealed class AcceptingAntiforgery : IAntiforgery
{
    private static readonly AntiforgeryTokenSet Tokens = new("request", "cookie", "form", "header");

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Tokens;

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Tokens;

    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;

    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
    }
}
