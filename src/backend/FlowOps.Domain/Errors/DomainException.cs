namespace FlowOps.Domain.Errors;

/// <summary>A business-rule violation. <see cref="Code"/> is one of <see cref="ErrorCodes"/>; the Api maps it to an HTTP status.</summary>
public sealed class DomainException(string code, string message, string? field = null) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>The camelCase input field a validation failure is about (becomes <c>errors.{field}</c>), if known.</summary>
    public string? Field { get; } = field;
}
