using FlowOps.Application.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Api.Auth;

/// <summary>Marks endpoints a user may call while <c>MustChangePassword</c> is set (the <c>/api/auth/*</c> family).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowedWhilePasswordChangeRequiredAttribute : Attribute;

/// <summary>
/// AUTHORIZATION.md §1.6: until an admin-set password is changed, everything except auth endpoints
/// (and anonymous ones) answers 403 <c>auth.password_change_required</c>.
/// </summary>
public sealed class PasswordChangeGate(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.MustChangePassword() && !IsExempt(context.GetEndpoint()))
        {
            throw new ForbiddenException(ErrorCodes.AuthPasswordChangeRequired, "You must change your password before continuing.");
        }

        return next(context);
    }

    private static bool IsExempt(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<AllowedWhilePasswordChangeRequiredAttribute>() is not null
        || endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null;
}
