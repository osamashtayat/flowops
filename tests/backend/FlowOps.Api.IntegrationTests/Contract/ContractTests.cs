using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;

namespace FlowOps.Api.IntegrationTests.Contract;

public sealed class ContractTests(ContractApiFactory factory) : IClassFixture<ContractApiFactory>
{
    private static readonly string CommittedDocumentPath = Path.Combine(TestPaths.RepositoryRoot, "contracts", "openapi.json");

    public static TheoryData<string> Endpoints => [.. ContractEndpoints.All.Select(e => e.ToString())];

    public static TheoryData<string> Stubs => [.. ContractEndpoints.All.Where(e => !e.Implemented).Select(e => e.ToString())];

    public static TheoryData<string> Protected => [.. ContractEndpoints.All.Where(e => !e.Anonymous).Select(e => e.ToString())];

    [Fact]
    public async Task CommittedOpenApi_MatchesWhatTheApiGenerates()
    {
        using var client = factory.ClientWithRoles();
        var generated = JsonNode.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken));
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(CommittedDocumentPath, TestContext.Current.CancellationToken));

        JsonNode.DeepEquals(committed, generated).ShouldBeTrue(
            "contracts/openapi.json is out of date: run ./scripts/export-openapi.sh and commit the result");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void CommittedOpenApi_DocumentsEveryContractEndpoint(string endpoint)
    {
        var (method, template) = Find(endpoint);
        var paths = JsonNode.Parse(File.ReadAllText(CommittedDocumentPath))!["paths"]!;

        paths[template]?[method.ToLowerInvariant()].ShouldNotBeNull($"{endpoint} is missing from contracts/openapi.json");
    }

    [Fact]
    public void CommittedOpenApi_HasNoUndocumentedEndpoints()
    {
        var documented = ContractEndpoints.All.Select(e => $"{e.Method} {e.Template}")
            .Append("GET /api/health")
            .ToHashSet(StringComparer.Ordinal);
        var paths = JsonNode.Parse(File.ReadAllText(CommittedDocumentPath))!["paths"]!.AsObject();

        var actual = paths.SelectMany(p => p.Value!.AsObject().Select(op => $"{op.Key.ToUpperInvariant()} {p.Key}"));

        actual.Where(e => !documented.Contains(e)).ShouldBeEmpty("every endpoint must be in docs/API_CONTRACT.md first");
    }

    [Theory]
    [MemberData(nameof(Stubs))]
    public async Task Endpoint_WithAuthorizedCallerAndValidInput_Returns501Stub(string endpoint)
    {
        var e = ContractEndpoints.All.Single(x => x.ToString() == endpoint);
        using var client = factory.ClientWithRoles(e.Roles);

        using var response = await SendAsync(client, e);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Protected))]
    public async Task Endpoint_Anonymous_Returns401Problem(string endpoint)
    {
        var e = ContractEndpoints.All.Single(x => x.ToString() == endpoint);
        using var client = factory.ClientWithRoles();

        using var response = await SendAsync(client, e);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await CodeOf(response)).ShouldBe(ErrorCodes.AuthUnauthenticated);
    }

    [Theory]
    [InlineData("POST /api/requests/purchase", Roles.Administrator)]
    [InlineData("POST /api/requests/leave", Roles.FinanceOfficer)]
    [InlineData("GET /api/tasks", Roles.Employee)]
    [InlineData("GET /api/tasks", Roles.Administrator)]
    [InlineData("POST /api/departments", Roles.Employee)]
    [InlineData("PUT /api/departments/{id}", Roles.HrOfficer)]
    [InlineData("GET /api/users", Roles.Employee)]
    [InlineData("POST /api/users/{id}/reset-password", Roles.FinanceOfficer)]
    [InlineData("GET /api/admin/stalled-tasks", Roles.Employee)]
    public async Task Endpoint_WithoutRequiredRole_Returns403Problem(string endpoint, string role)
    {
        var e = ContractEndpoints.All.Single(x => x.ToString() == endpoint);
        using var client = factory.ClientWithRoles(role);

        using var response = await SendAsync(client, e);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOf(response)).ShouldBe(ErrorCodes.AuthForbidden);
    }

    [Fact]
    public async Task UnknownRoute_Authenticated_Returns404Problem()
    {
        using var client = factory.ClientWithRoles(Roles.Employee);

        using var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOf(response)).ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        using var client = factory.ClientWithRoles();

        using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidInput_IsRejectedBeforeTheStub()
    {
        using var client = factory.ClientWithRoles(Roles.Employee);
        using var content = new StringContent(
            """{"itemName":"","description":"d","estimatedCost":{"amount":"1.0005","currency":"USD"},"justification":"j"}""",
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/requests/purchase", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["estimatedCost.amount", "estimatedCost.currency", "itemName"], ignoreOrder: true);
    }

    [Fact]
    public void RoleEnum_MatchesDomainRoles() =>
        Enum.GetNames<Role>().ShouldBe(Roles.All, ignoreOrder: true);

    private static (string Method, string Template) Find(string endpoint)
    {
        var e = ContractEndpoints.All.Single(x => x.ToString() == endpoint);
        return (e.Method, e.Template);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, ContractEndpoint e)
    {
        using var request = new HttpRequestMessage(new HttpMethod(e.Method), new Uri(e.Url, UriKind.Relative));
        if (e.Body is not null)
        {
            request.Content = new StringContent(e.Body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.GetProperty("code").GetString();
    }
}
