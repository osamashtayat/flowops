using System.Globalization;

namespace FlowOps.Application.Common;

/// <summary>The wire form of a PostgreSQL <c>xmin</c> concurrency token: an opaque string (API_CONTRACT.md §1).</summary>
public static class VersionToken
{
    /// <summary>Generous for a <c>uint</c> (10 digits); longer input is a 400, not something worth parsing.</summary>
    public const int MaxLength = 20;

    public static string Format(uint version) => version.ToString(CultureInfo.InvariantCulture);

    /// <summary>False for anything the server never issued; callers treat that as a stale version (409).</summary>
    public static bool TryParse(string? token, out uint version) =>
        uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out version);
}
