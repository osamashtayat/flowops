using FlowOps.Application.Common;

namespace FlowOps.Application.Organization;

/// <summary><c>GET /api/auth/me</c> and the login response. The UI renders navigation from <see cref="Capabilities"/>.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    DepartmentRef? Department,
    UserRef? Manager,
    IReadOnlyList<Role> Roles,
    IReadOnlyList<Capability> Capabilities,
    bool MustChangePassword);
