using System.Security.Claims;
using FlowOps.Application.Common;

namespace FlowOps.Api.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> from the authenticated principal: the only way services read claims.
/// (Policies and the password-change gate read the FlowOps claims via <see cref="ClaimsPrincipalExtensions"/>.)
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid Id =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("There is no authenticated user with a valid id on this request.");

    public IReadOnlySet<string> Roles =>
        IsAuthenticated
            ? Principal!.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    public bool IsInRole(string role) => IsAuthenticated && Principal!.IsInRole(role);
}
