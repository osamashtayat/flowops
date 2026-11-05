using FlowOps.Domain.Errors;

namespace FlowOps.Application.Common;

/// <summary>
/// Input that passed DataAnnotations but was rejected by a rule only the server can check
/// (e.g. Identity password rules). Becomes a 400 <c>validation</c> problem with camelCase field <see cref="Errors"/>.
/// </summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : AppException(ErrorCodes.Validation, "One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationFailedException For(string field, params string[] messages) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = messages });
}
