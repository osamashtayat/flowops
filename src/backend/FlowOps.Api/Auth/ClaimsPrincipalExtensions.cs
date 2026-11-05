using System.Security.Claims;
using FlowOps.Application.Common;
using FlowOps.Application.Organization;

namespace FlowOps.Api.Auth;

/// <summary>Reads the FlowOps session claims for policies and middleware (services use <see cref="ICurrentUser"/>).</summary>
public static class ClaimsPrincipalExtensions
{
    public static bool HasCapability(this ClaimsPrincipal user, Capability capability)
    {
        ArgumentNullException.ThrowIfNull(user);
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        return CapabilityRules.For(roles, user.HasFlag(AppClaimTypes.IsManager)).Contains(capability);
    }

    public static bool MustChangePassword(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.HasFlag(AppClaimTypes.MustChangePassword);
    }

    private static bool HasFlag(this ClaimsPrincipal user, string type) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(type, AppClaimTypes.True);
}
