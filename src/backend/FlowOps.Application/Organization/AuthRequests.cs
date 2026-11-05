using System.ComponentModel.DataAnnotations;

namespace FlowOps.Application.Organization;

/// <summary><c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(UserLimits.EmailMaxLength, MinimumLength = 1)]
    public string Email { get; init; } = "";

    [Required]
    [StringLength(UserLimits.PasswordMaxLength, MinimumLength = 1)]
    public string Password { get; init; } = "";
}

/// <summary><c>POST /api/auth/change-password</c>. Identity password rules map to <c>errors.newPassword</c>.</summary>
public sealed record ChangePasswordRequest
{
    [Required]
    [StringLength(UserLimits.PasswordMaxLength, MinimumLength = 1)]
    public string CurrentPassword { get; init; } = "";

    [Required]
    [StringLength(UserLimits.PasswordMaxLength, MinimumLength = UserLimits.PasswordMinLength)]
    public string NewPassword { get; init; } = "";
}
