namespace FlowOps.Application.Common;

/// <summary>Sign-in was refused (<c>auth.invalid_credentials</c> → 401, also while locked out).</summary>
public sealed class AuthenticationFailedException(string code, string message) : AppException(code, message);
