namespace FlowOps.Domain.Common;

/// <summary>Currencies <see cref="Money"/> accepts. Phase 1 is JOD only.</summary>
public static class SupportedCurrencies
{
    public const string Jod = "JOD";

    public static IReadOnlyList<string> All { get; } = [Jod];

    public static bool IsSupported(string currency) => All.Contains(currency, StringComparer.Ordinal);
}
