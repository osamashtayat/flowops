using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>One workflow per request type (DOMAIN_MODEL.md §7.1, ADR-0014 Q4).</summary>
internal sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("workflow_definitions", t =>
            t.HasCheckConstraint("ck_workflow_definitions_request_type", CheckConstraints.InEnum<RequestType>("request_type")));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.RequestType).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(d => d.Name).HasMaxLength(WorkflowDefinition.NameMaxLength);
        builder.HasIndex(d => d.RequestType).IsUnique().HasDatabaseName("ux_workflow_definitions_request_type");
    }
}

/// <summary>
/// Versions and their owned steps (DOMAIN_MODEL.md §7.1). The database backs "exactly one Published, at most one Draft
/// per workflow" with partial unique indexes, and the lifecycle fields' consistency with check constraints.
/// </summary>
internal sealed class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public const string PublishedIndex = "ux_workflow_versions_published";
    public const string DraftIndex = "ux_workflow_versions_draft";

    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.ToTable("workflow_versions", t =>
        {
            t.HasCheckConstraint("ck_workflow_versions_status", CheckConstraints.InEnum<WorkflowVersionStatus>("status"));
            t.HasCheckConstraint("ck_workflow_versions_number", "number >= 1");
            t.HasCheckConstraint("ck_workflow_versions_published_iff_not_draft", "(status = 'Draft') = (published_at IS NULL)");
            t.HasCheckConstraint("ck_workflow_versions_retired_iff_retired", "(status = 'Retired') = (retired_at IS NOT NULL)");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(v => v.Version).IsRowVersion();

        builder.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(v => v.WorkflowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(v => v.PublishedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.WorkflowId, v.Number }).IsUnique().HasDatabaseName("ux_workflow_versions_number");
        builder.HasIndex(v => v.WorkflowId, PublishedIndex).IsUnique().HasFilter("status = 'Published'").HasDatabaseName(PublishedIndex);
        builder.HasIndex(v => v.WorkflowId, DraftIndex).IsUnique().HasFilter("status = 'Draft'").HasDatabaseName(DraftIndex);
        builder.HasIndex(v => v.CreatedById);
        builder.HasIndex(v => v.PublishedById);

        builder.OwnsMany(v => v.Steps, ConfigureSteps);
        builder.Navigation(v => v.Steps).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_steps");
        builder.Ignore(v => v.FirstStep);
    }

    private static void ConfigureSteps(OwnedNavigationBuilder<WorkflowVersion, WorkflowStep> step)
    {
        step.ToTable("workflow_steps", t =>
        {
            t.HasCheckConstraint("ck_workflow_steps_order", "\"order\" >= 0");
            t.HasCheckConstraint("ck_workflow_steps_kind", CheckConstraints.InEnum<WorkTaskKind>("kind"));
            t.HasCheckConstraint("ck_workflow_steps_assignee_kind", CheckConstraints.InEnum<AssigneeRuleKind>("assignee_kind"));
            // Role and RequesterManager are the Phase 1 rules; P2-BE-06 widens this with its new kinds.
            t.HasCheckConstraint("ck_workflow_steps_assignee_role",
                $"(assignee_kind = 'Role') = (assignee_role IS NOT NULL) AND assignee_role <> '{Roles.Administrator}'");
        });
        step.WithOwner().HasForeignKey("WorkflowVersionId");
        step.HasKey("WorkflowVersionId", nameof(WorkflowStep.Order));
        step.Property(s => s.Order).ValueGeneratedNever();
        step.Property(s => s.Key).HasMaxLength(WorkflowStep.KeyMaxLength);
        step.Property(s => s.Name).HasMaxLength(WorkflowStep.NameMaxLength);
        step.Property(s => s.Kind).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        step.Property(s => s.AssigneeKind).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        step.Property(s => s.AssigneeRole).HasMaxLength(CheckConstraints.EnumMaxLength);
        step.HasIndex("WorkflowVersionId", nameof(WorkflowStep.Key)).IsUnique().HasDatabaseName("ux_workflow_steps_key");
        step.Ignore(s => s.Assignee);
        step.Ignore(s => s.Stage);
    }
}
