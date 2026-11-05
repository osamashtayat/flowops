using FlowOps.Application.Common;
using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Organization;

/// <summary>Departments (API_CONTRACT.md §8). Duplicate names/codes are caught by unique indexes and surface as 409.</summary>
public sealed class DepartmentService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    /// <summary>Everyone sees active departments (filters need them); only administrators can include inactive ones.</summary>
    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(DepartmentListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var includeInactive = query.IncludeInactive && CapabilityRules.CanAdminister(currentUser.Roles);
        // Ordered before projecting: EF can't sort on a member of a constructor-built DTO.
        return await Project(db.Departments.Where(d => includeInactive || d.IsActive).OrderBy(d => d.Name).ThenBy(d => d.Id))
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var department = Department.Create(body.Name, body.Code, time.GetUtcNow());
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(department.Id, department.Name, department.Code, department.IsActive, MemberCount: 0);
    }

    public async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException();
        department.Update(body.Name, body.Code, body.IsActive);
        await db.SaveChangesAsync(ct);
        return await Project(db.Departments.Where(d => d.Id == id)).SingleAsync(ct);
    }

    /// <summary><c>memberCount</c> counts active members: the people the department currently has.</summary>
    private IQueryable<DepartmentDto> Project(IQueryable<Department> departments) => departments.AsNoTracking()
        .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.IsActive, db.Users.Count(u => u.DepartmentId == d.Id && u.IsActive)));
}
