using FlowOps.Application.Common;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Infrastructure.Persistence;

/// <summary>The single EF Core context: Identity stores plus FlowOps aggregates (configurations added per task).</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    /// <summary>Feeds <c>Request.Number</c> (DOMAIN_MODEL.md §2.3); read by the number generator (BE-10).</summary>
    public const string RequestNumberSequence = "request_number_seq";

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<Notification> Notifications => Set<Notification>();

    IQueryable<UserReadModel> IAppDbContext.Users => Users.Select(u => new UserReadModel
    {
        Id = u.Id,
        Email = u.Email!,
        DisplayName = u.DisplayName,
        DepartmentId = u.DepartmentId,
        ManagerId = u.ManagerId,
        IsActive = u.IsActive,
        MustChangePassword = u.MustChangePassword,
        CreatedAt = u.CreatedAt,
    });

    IQueryable<UserRoleReadModel> IAppDbContext.UserRoles =>
        UserRoles.Join(Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new UserRoleReadModel { UserId = ur.UserId, RoleName = r.Name! });

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        IdentityTableNames.Apply(builder);
        builder.HasSequence<long>(RequestNumberSequence);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
