namespace FlowOps.Application.Common;

/// <summary>
/// FlowOps-specific claims in the session cookie. Infrastructure writes them when it builds the principal
/// (refreshed on every security-stamp validation); the Api reads them for policies and the password-change gate.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>Present ("true") when the user has at least one active direct report.</summary>
    public const string IsManager = "flowops:is_manager";

    /// <summary>Present ("true") until the user changes an admin-set password.</summary>
    public const string MustChangePassword = "flowops:must_change_password";

    public const string True = "true";
}
