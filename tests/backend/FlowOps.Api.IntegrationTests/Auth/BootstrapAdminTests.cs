using System.Net;
using System.Text.Json;
using FlowOps.Api.IntegrationTests.Harness;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Auth;

/// <summary>AUTHORIZATION.md §1.6: the first Administrator comes from configuration, once.</summary>
public sealed class BootstrapAdminTests(FlowOpsApiFactory factory) : DatabaseTest(factory)
{
    private const string Email = "root@flowops.test";
    private const string Password = "Bootstrap-Pa55word";

    [Fact]
    public async Task Startup_WithConfigAndNoAdmin_CreatesAdminWhoMustChangePassword()
    {
        await using var app = WithBootstrapConfig();
        _ = app.Services; // starts the host, which runs the seeder

        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await FlowOpsApiFactory.LoginAsync(client, Email, Password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        json.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe([Roles.Administrator]);
        json.RootElement.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Startup_Twice_CreatesOnlyOneAdmin()
    {
        await using (var first = WithBootstrapConfig())
        {
            _ = first.Services;
        }

        await using var second = WithBootstrapConfig();
        _ = second.Services;

        (await AdministratorsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Startup_WhenAnAdminExists_CreatesNoOther()
    {
        await Given.User("ada").WithRoles(Roles.Administrator).CreateAsync(Factory);

        await using var app = WithBootstrapConfig();
        _ = app.Services;

        (await AdministratorsAsync()).Select(u => u.Email).ShouldBe(["ada@flowops.test"]);
    }

    [Fact]
    public async Task Startup_WithoutConfig_CreatesNothing()
    {
        // The shared factory has no bootstrap configuration and has already started.
        (await AdministratorsAsync()).ShouldBeEmpty();
    }

    private WebApplicationFactory<Program> WithBootstrapConfig() =>
        Factory.WithWebHostBuilder(b => b
            .UseSetting("FlowOps:BootstrapAdmin:Email", Email)
            .UseSetting("FlowOps:BootstrapAdmin:Password", Password));

    private async Task<IList<ApplicationUser>> AdministratorsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetUsersInRoleAsync(Roles.Administrator);
    }
}
