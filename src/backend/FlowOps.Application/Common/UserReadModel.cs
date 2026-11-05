namespace FlowOps.Application.Common;

/// <summary>A user as queries see it (DOMAIN_MODEL.md §2.2). Projected from the Identity user by Infrastructure.</summary>
public sealed record UserReadModel
{
    public Guid Id { get; init; }

    public string Email { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public Guid? DepartmentId { get; init; }

    public Guid? ManagerId { get; init; }

    public bool IsActive { get; init; }

    public bool MustChangePassword { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>One role a user holds.</summary>
public sealed record UserRoleReadModel
{
    public Guid UserId { get; init; }

    public string RoleName { get; init; } = "";
}
