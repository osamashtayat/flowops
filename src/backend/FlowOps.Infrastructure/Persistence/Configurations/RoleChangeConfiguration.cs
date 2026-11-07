using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>ADR-0012 §4. The database backs "one pending change per user" and the decision fields' consistency.</summary>
internal sealed class RoleChangeConfiguration : IEntityTypeConfiguration<RoleChange>
{
    /// <summary>Translated to <c>role_change.already_pending</c> by <see cref="AppDbContext"/>.</summary>
    public const string PendingPerUserIndex = "ux_role_changes_pending_per_user";

    public void Configure(EntityTypeBuilder<RoleChange> builder)
    {
        builder.ToTable("role_changes", t =>
        {
            t.HasCheckConstraint("ck_role_changes_status", CheckConstraints.InEnum<RoleChangeStatus>("status"));
            t.HasCheckConstraint("ck_role_changes_decision_iff_closed", "(status = 'Pending') = (decided_at IS NULL)");
            t.HasCheckConstraint("ck_role_changes_reason_iff_rejected", "(status = 'Rejected') = (rejection_reason IS NOT NULL)");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(c => c.PreviousRoles).IsRequired();
        builder.Property(c => c.ProposedRoles).IsRequired();
        builder.Property(c => c.RejectionReason).HasMaxLength(RoleChange.RejectionReasonMaxLength);
        builder.Property(c => c.Version).IsRowVersion();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.DecidedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.UserId, PendingPerUserIndex).IsUnique().HasFilter("status = 'Pending'").HasDatabaseName(PendingPerUserIndex);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.DecidedById);
        // The admin list: newest first, optionally by status.
        builder.HasIndex(c => c.ProposedAt).IsDescending();
    }
}
