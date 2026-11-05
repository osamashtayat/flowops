using System.Globalization;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>
/// Builds <c>CHECK</c> SQL from the C# source of truth (DOMAIN_MODEL.md §6: "check constraints generated per enum"),
/// so adding an enum value or currency changes the constraint in the next migration instead of drifting.
/// </summary>
internal static class CheckConstraints
{
    public const int EnumMaxLength = 32;

    public static string InEnum<TEnum>(string column)
        where TEnum : struct, Enum => In(column, Enum.GetNames<TEnum>());

    /// <summary><c>column IN (...)</c>. A NULL column passes, so nullable columns need no extra clause.</summary>
    public static string In(string column, IEnumerable<string> values) =>
        string.Create(CultureInfo.InvariantCulture, $"{column} IN ({string.Join(", ", values.Select(Quote))})");

    private static string Quote(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
            ? $"'{value}'"
            : throw new ArgumentException($"'{value}' is not safe to inline in a check constraint.", nameof(value));
}
