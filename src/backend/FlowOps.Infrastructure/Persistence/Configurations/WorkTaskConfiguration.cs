using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FlowOps.Domain.Workflows;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>DOMAIN_MODEL.md §2.5, §6. The database backs I3 (<c>xmin</c>) and I4 (one open task per request).</summary>
internal sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public const int AssigneeRoleMaxLength = 32;
    private const string OpenFilter = "status = 'Open'";

    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("work_tasks", t =>
        {
            t.HasCheckConstraint("ck_work_tasks_kind", CheckConstraints.InEnum<WorkTaskKind>("kind"));
            t.HasCheckConstraint("ck_work_tasks_status", CheckConstraints.InEnum<WorkTaskStatus>("status"));
            t.HasCheckConstraint("ck_work_tasks_outcome", CheckConstraints.InEnum<WorkTaskOutcome>("outcome"));
            t.HasCheckConstraint("ck_work_tasks_assignee_role", CheckConstraints.In("assignee_role", Roles.All));
            t.HasCheckConstraint("ck_work_tasks_one_assignee", "num_nonnulls(assignee_user_id, assignee_role) = 1");
            // Outcome, decider and time are set together, exactly when the task is Done (§2.5 "null iff Open").
            t.HasCheckConstraint(
                "ck_work_tasks_decision_iff_done",
                "(status = 'Open') = (outcome IS NULL AND decided_by_id IS NULL AND decided_at IS NULL)");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(t => t.StepKey).HasMaxLength(WorkflowStep.KeyMaxLength);
        builder.Property(t => t.StepName).HasMaxLength(WorkflowStep.NameMaxLength);
        builder.Ignore(t => t.Step);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(t => t.Outcome).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(t => t.AssigneeRole).HasMaxLength(AssigneeRoleMaxLength);
        builder.Property(t => t.Comment).HasMaxLength(RequestLimits.CommentMaxLength);
        builder.Property(t => t.Version).IsRowVersion();
        builder.Ignore(t => t.IsOpen);
        builder.Ignore(t => t.AllowedActions);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.DecidedById).OnDelete(DeleteBehavior.Restrict);

        // Named indexes, because request_id carries two: the partial unique one only covers open tasks.
        // (HasDatabaseName as well: the snake_case convention otherwise renames named indexes.)
        Named(builder.HasIndex(t => t.RequestId, "ux_work_tasks_open_per_request")).IsUnique().HasFilter(OpenFilter);
        Named(builder.HasIndex(t => t.RequestId, "ix_work_tasks_request_id"));
        Named(builder.HasIndex(t => t.AssigneeUserId, "ix_work_tasks_open_assignee_user_id")).HasFilter(OpenFilter);
        Named(builder.HasIndex(t => t.AssigneeRole, "ix_work_tasks_open_assignee_role")).HasFilter(OpenFilter);
        // The FK's own index; also serves the inbox's "decided by me" tab.
        builder.HasIndex(t => t.DecidedById);
    }

    private static IndexBuilder<WorkTask> Named(IndexBuilder<WorkTask> index) => index.HasDatabaseName(index.Metadata.Name);
}
