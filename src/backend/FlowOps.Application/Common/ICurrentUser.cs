namespace FlowOps.Application.Common;

/// <summary>
/// The authenticated actor, from server-side claims. Services take identity from here and never
/// from request bodies (AUTHORIZATION.md, I14).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The user's id. Throws when <see cref="IsAuthenticated"/> is false.</summary>
    Guid Id { get; }

    IReadOnlySet<string> Roles { get; }

    bool IsInRole(string role);
}
