namespace FlowOps.Application.Access;

/// <summary>
/// Who is asking, as resource checks see them (AUTHORIZATION.md §4). Built from the database by
/// <see cref="AccessRules.ContextForAsync"/>, not from session claims, so resource checks are never stale.
/// </summary>
public sealed record AccessContext(Guid UserId, IReadOnlySet<string> Roles, bool IsActive)
{
    /// <summary>For EF translation: <c>Contains</c> on an array parameter becomes <c>= ANY(@roles)</c>.</summary>
    internal string[] RoleArray { get; } = [.. Roles];
}
