namespace FlowOps.Api.Auth;

/// <summary>
/// Coarse authorization policies (AUTHORIZATION.md §6), each one a capability from <c>CapabilityRules</c> so the
/// server enforces exactly what <c>/api/auth/me</c> tells the UI. Resource checks live in Application.
/// </summary>
public static class Policies
{
    /// <summary>Administration endpoints (API_CONTRACT.md §8).</summary>
    public const string Admin = nameof(Admin);

    /// <summary>Submitting requests needs the Employee role, whatever other roles the user holds.</summary>
    public const string CanSubmit = nameof(CanSubmit);

    /// <summary>The inbox: any officer role, or a manager (derived from active direct reports).</summary>
    public const string TasksView = nameof(TasksView);
}
