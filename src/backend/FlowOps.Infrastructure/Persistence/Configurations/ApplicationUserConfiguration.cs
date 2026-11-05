using FlowOps.Application.Organization;
using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>The FlowOps profile columns on <c>users</c> (DOMAIN_MODEL.md §2.2, §6). Table/index names: <see cref="IdentityTableNames"/>.</summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(UserLimits.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        builder.HasOne<Department>().WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.DepartmentId);
        builder.HasIndex(u => u.ManagerId);

        // I13 (no self-management) is enforced by the database too; cycles are checked in Application (BE-08).
        builder.ToTable(t => t.HasCheckConstraint("ck_users_manager_not_self", "manager_id <> id"));
    }
}
