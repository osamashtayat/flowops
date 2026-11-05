using FlowOps.Domain.Errors;

namespace FlowOps.Application.Common;

/// <summary>The caller can see the resource but may not perform the action (403).</summary>
public sealed class ForbiddenException(string code = ErrorCodes.AuthForbidden, string message = "You are not allowed to do this.")
    : AppException(code, message);
