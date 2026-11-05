using System.ComponentModel.DataAnnotations;
using FlowOps.Domain.Organization;

namespace FlowOps.Application.Organization;

public sealed record DepartmentDto(Guid Id, string Name, string Code, bool IsActive, int MemberCount);

/// <summary><c>GET /api/departments</c>. Any authenticated user may list active departments (used in filters).</summary>
public sealed record DepartmentListQuery
{
    public bool IncludeInactive { get; init; }
}

/// <summary><c>POST /api/departments</c>. The code is upper-cased by <see cref="Department.Create"/>.</summary>
public sealed record CreateDepartmentRequest
{
    public const string CodePattern = "^[A-Za-z0-9-]+$";

    [Required]
    [StringLength(Department.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = "";

    [Required]
    [StringLength(Department.CodeMaxLength, MinimumLength = 1)]
    [RegularExpression(CodePattern)]
    public string Code { get; init; } = "";
}

/// <summary><c>PUT /api/departments/{id}</c>.</summary>
public sealed record UpdateDepartmentRequest
{
    [Required]
    [StringLength(Department.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = "";

    [Required]
    [StringLength(Department.CodeMaxLength, MinimumLength = 1)]
    [RegularExpression(CreateDepartmentRequest.CodePattern)]
    public string Code { get; init; } = "";

    public bool IsActive { get; init; }
}
