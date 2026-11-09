using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FlowOps.Api.Errors;
using FlowOps.Domain.Errors;

namespace FlowOps.Api.IntegrationTests.Errors;

public sealed class ProblemDetailsTests(ErrorPipelineFactory factory) : IClassFixture<ErrorPipelineFactory>
{
    private static readonly string[] AllErrorCodes = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    // The contract table (API_CONTRACT.md §2), restated here so a change to either side is caught.
    public static TheoryData<string, int> ContractStatuses => new()
    {
        { ErrorCodes.Validation, 400 },
        { ErrorCodes.AuthUnauthenticated, 401 },
        { ErrorCodes.AuthInvalidCredentials, 401 },
        { ErrorCodes.AuthForbidden, 403 },
        { ErrorCodes.AuthPasswordChangeRequired, 403 },
        { ErrorCodes.TaskNotEligible, 403 },
        { ErrorCodes.TaskSelfDecision, 403 },
        { ErrorCodes.TaskAlreadyParticipated, 403 },
        { ErrorCodes.NotFound, 404 },
        { ErrorCodes.TaskAlreadyDecided, 409 },
        { ErrorCodes.TaskNotCurrent, 409 },
        { ErrorCodes.TaskInvalidAction, 409 },
        { ErrorCodes.RequestClosed, 409 },
        { ErrorCodes.ConcurrencyConflict, 409 },
        { ErrorCodes.DepartmentDuplicate, 409 },
        { ErrorCodes.UserDuplicateEmail, 409 },
        { ErrorCodes.RequestManagerNotAssigned, 422 },
        { ErrorCodes.RequestDepartmentNotAssigned, 422 },
        { ErrorCodes.UserManagerCycle, 422 },
        { ErrorCodes.UserLastAdmin, 422 },
        { ErrorCodes.UserSelfAdministration, 422 },
        { ErrorCodes.RequestCommentRateLimited, 429 },
        { ErrorCodes.DepartmentInactive, 422 },
        { ErrorCodes.UserAdminRoleOnCreateOnly, 422 },
        { ErrorCodes.RoleChangeNoReviewer, 422 },
        { ErrorCodes.RoleChangeApproverTooNew, 422 },
        { ErrorCodes.RoleChangeSelfApproval, 422 },
        { ErrorCodes.RoleChangeAlreadyPending, 409 },
        { ErrorCodes.RoleChangeNotPending, 409 },
        { ErrorCodes.RoleChangeStale, 409 },
        { ErrorCodes.RoleChangeNotProposer, 403 },
        { ErrorCodes.AuthRateLimited, 429 },
        { ErrorCodes.ServerError, 500 },
    };

    [Fact]
    public void StatusTable_CoversEveryErrorCode()
    {
        ErrorStatusCodes.MappedCodes.Order(StringComparer.Ordinal).ShouldBe(AllErrorCodes.Order(StringComparer.Ordinal));
        ContractStatuses.Select(row => row.Data.Item1).Order(StringComparer.Ordinal)
            .ShouldBe(AllErrorCodes.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ContractStatuses))]
    public void StatusTable_MatchesContract(string code, int status) => ErrorStatusCodes.For(code).ShouldBe(status);

    [Theory]
    [MemberData(nameof(ContractStatuses))]
    public async Task DomainException_MapsToContractStatusAndShape(string code, int status)
    {
        using var response = await GetAsync("/test/errors/domain/" + code);

        var problem = await ShouldBeProblemAsync(response, status, code);
        if (status < 500)
        {
            problem.GetProperty("detail").GetString().ShouldBe("Domain detail for " + code);
        }
    }

    [Fact]
    public async Task NotFoundException_Returns404NotFound()
    {
        using var response = await GetAsync("/test/errors/not-found");

        await ShouldBeProblemAsync(response, 404, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ForbiddenException_Returns403WithItsCode()
    {
        using var response = await GetAsync("/test/errors/forbidden");

        var problem = await ShouldBeProblemAsync(response, 403, ErrorCodes.TaskNotEligible);
        problem.GetProperty("detail").GetString().ShouldBe("Not your task.");
    }

    [Fact]
    public async Task ConflictException_Returns409WithItsCode()
    {
        using var response = await GetAsync("/test/errors/conflict");

        await ShouldBeProblemAsync(response, 409, ErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithoutInternals()
    {
        using var response = await GetAsync("/test/errors/unhandled");

        var problem = await ShouldBeProblemAsync(response, 500, ErrorCodes.ServerError);
        var raw = problem.GetRawText();
        raw.ShouldNotContain("hunter2");
        raw.ShouldNotContain("db-internal-7");
        raw.ShouldNotContain("InvalidOperationException");
        problem.TryGetProperty("exception", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task UnknownCode_Returns500ServerError()
    {
        using var response = await GetAsync("/test/errors/domain/made.up_code");

        await ShouldBeProblemAsync(response, 500, ErrorCodes.ServerError);
    }

    [Fact]
    public async Task ModelValidationFailure_Returns400ValidationWithCamelCaseFieldErrors()
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri("/test/errors/validation", UriKind.Relative), new { itemName = "far too long" }, TestContext.Current.CancellationToken);

        var problem = await ShouldBeProblemAsync(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldContain("itemName");
    }

    [Fact]
    public async Task ValidationFailedException_Returns400WithFieldErrors()
    {
        using var response = await GetAsync("/test/errors/validation-failed");

        var problem = await ShouldBeProblemAsync(response, 400, ErrorCodes.Validation);
        problem.GetProperty("errors").GetProperty("newPassword")[0].GetString().ShouldBe("Passwords must have at least one digit.");
    }

    [Fact]
    public async Task UnknownRoute_Anonymous_Returns401Problem()
    {
        // Authenticated-by-default fallback policy: anonymous callers learn nothing about which routes exist.
        using var response = await GetAsync("/api/does-not-exist");

        await ShouldBeProblemAsync(response, 401, ErrorCodes.AuthUnauthenticated);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var client = CreateClient();
        return await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ShouldBeProblemAsync(HttpResponseMessage response, int status, string code)
    {
        ((int)response.StatusCode).ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
        json.GetProperty("status").GetInt32().ShouldBe(status);
        json.GetProperty("code").GetString().ShouldBe(code);
        json.GetProperty("type").GetString().ShouldBe("https://flowops.local/problems/" + code);
        json.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        json.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        return json;
    }
}
