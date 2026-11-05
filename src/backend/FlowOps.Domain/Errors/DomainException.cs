namespace FlowOps.Domain.Errors;

/// <summary>A business-rule violation. <see cref="Code"/> is one of <see cref="ErrorCodes"/>; the Api maps it to an HTTP status.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
