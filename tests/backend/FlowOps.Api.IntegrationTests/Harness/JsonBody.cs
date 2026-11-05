using System.Text.Json;

namespace FlowOps.Api.IntegrationTests.Harness;

public static class JsonBody
{
    /// <summary>The response body as a detached JSON element.</summary>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.Clone();
    }
}
