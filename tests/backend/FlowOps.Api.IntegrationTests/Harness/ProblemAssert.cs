using System.Text.Json;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>Asserts an error response by status <b>and</b> <c>code</c> (API_CONTRACT.md §2).</summary>
public static class ProblemAssert
{
    public static async Task<JsonElement> HasCode(HttpResponseMessage response, int status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("code").GetString().ShouldBe(code, body);
        return json.RootElement.Clone();
    }
}
