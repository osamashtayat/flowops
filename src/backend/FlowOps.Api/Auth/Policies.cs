namespace FlowOps.Api.Auth;

/// <summary>Coarse, role-level authorization policies (AUTHORIZATION.md §6). Resource checks live in Application.</summary>
public static class Policies
{
    /// <summary>Administration endpoints (API_CONTRACT.md §8).</summary>
    public const string Admin = nameof(Admin);

    /// <summary>Submitting requests needs the Employee role, whatever other roles the user holds.</summary>
    public const string CanSubmit = nameof(CanSubmit);

    /// <summary>The inbox: any officer role. BE-05 adds the derived <c>isManager</c> capability.</summary>
    public const string TasksView = nameof(TasksView);
}
