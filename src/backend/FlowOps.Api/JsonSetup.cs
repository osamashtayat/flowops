using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowOps.Api;

/// <summary>Wire format rules (API_CONTRACT.md conventions), applied to MVC and to the OpenAPI generator alike.</summary>
public static class JsonSetup
{
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Enums travel as strings only: an integer such as 99 would otherwise bind to an undefined value.
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // Numbers are numbers: the Web default also accepts "1", which would type every integer as number | string.
        options.NumberHandling = JsonNumberHandling.Strict;
    }
}
