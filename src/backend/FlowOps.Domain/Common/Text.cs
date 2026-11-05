using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Common;

/// <summary>Required/optional free-text validation shared by request details, decisions and comments.</summary>
internal static class Text
{
    internal static string Required(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw DomainErrors.Validation($"{field} is required.", field);
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw DomainErrors.Validation($"{field} must be at most {maxLength} characters.", field);
    }

    internal static string? Optional(string? value, int maxLength, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maxLength, field);
}
