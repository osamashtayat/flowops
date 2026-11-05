namespace FlowOps.Application.Common;

/// <summary>Sign-in was refused (<c>auth.invalid_credentials</c> → 401, <c>auth.locked_out</c> → 423).</summary>
public sealed class AuthenticationFailedException(string code, string message) : AppException(code, message);
