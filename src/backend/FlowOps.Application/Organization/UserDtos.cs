using System.ComponentModel.DataAnnotations;
using FlowOps.Application.Common;

namespace FlowOps.Application.Organization;

/// <summary>A user for administrators. Explicit fields only: no hashes, stamps or lockout data.</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    DepartmentRef? Department,
    UserRef? Manager,
    IReadOnlyList<Role> Roles,
    int DirectReportCount,
    DateTimeOffset CreatedAt);

/// <summary><c>GET /api/users</c>.</summary>
public sealed record UserListQuery : PageRequest
{
    [StringLength(UserLimits.SearchMaxLength)]
    public string? Search { get; init; }

    public Guid? DepartmentId { get; init; }

    public Role? Role { get; init; }

    public bool IncludeInactive { get; init; }
}

/// <summary><c>POST /api/users</c>. The account starts with MustChangePassword = true.</summary>
public sealed record CreateUserRequest
{
    [Required]
    [EmailAddress]
    [StringLength(UserLimits.EmailMaxLength, MinimumLength = 1)]
    public string Email { get; init; } = "";

    [Required]
    [StringLength(UserLimits.DisplayNameMaxLength, MinimumLength = 1)]
    public string DisplayName { get; init; } = "";

    [Required]
    [StringLength(UserLimits.PasswordMaxLength, MinimumLength = UserLimits.PasswordMinLength)]
    public string InitialPassword { get; init; } = "";

    public Guid? DepartmentId { get; init; }

    public Guid? ManagerId { get; init; }

    [Required]
    public IReadOnlyList<Role> Roles { get; init; } = [];
}

/// <summary><c>PUT /api/users/{id}</c>.</summary>
public sealed record UpdateUserRequest
{
    [Required]
    [StringLength(UserLimits.DisplayNameMaxLength, MinimumLength = 1)]
    public string DisplayName { get; init; } = "";

    public Guid? DepartmentId { get; init; }

    public Guid? ManagerId { get; init; }

    public bool IsActive { get; init; }
}

/// <summary><c>PUT /api/users/{id}/roles</c>: replaces the user's roles.</summary>
public sealed record UpdateUserRolesRequest
{
    [Required]
    public IReadOnlyList<Role> Roles { get; init; } = [];
}

/// <summary><c>POST /api/users/{id}/reset-password</c>: sets MustChangePassword.</summary>
public sealed record ResetPasswordRequest
{
    [Required]
    [StringLength(UserLimits.PasswordMaxLength, MinimumLength = UserLimits.PasswordMinLength)]
    public string NewPassword { get; init; } = "";
}
