namespace FlowOps.Application.Common;

/// <summary>
/// Base for Application-layer failures. Like <c>DomainException</c>, it carries a stable code from
/// <c>ErrorCodes</c>; the Api's exception handler derives the HTTP status from that code.
/// </summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
