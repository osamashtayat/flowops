namespace FlowOps.Application.Common;

/// <summary>Valid input that breaks a business precondition only the server can check, e.g. <c>user.last_admin</c> (422).</summary>
public sealed class BusinessRuleException(string code, string message) : AppException(code, message);
