using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FlowOps.Domain.Workflows;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>DOMAIN_MODEL.md §2.6: append-only history; comments are activities of type <c>CommentAdded</c>.</summary>
internal sealed class RequestActivityConfiguration : IEntityTypeConfiguration<RequestActivity>
{
    public void Configure(EntityTypeBuilder<RequestActivity> builder)
    {
        builder.ToTable("request_activities", t =>
        {
            t.HasCheckConstraint("ck_request_activities_type", CheckConstraints.InEnum<ActivityType>("type"));
            t.HasCheckConstraint("ck_request_activities_step", "(step_key IS NULL) = (step_name IS NULL)");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(a => a.StepKey).HasMaxLength(WorkflowStep.KeyMaxLength);
        builder.Property(a => a.StepName).HasMaxLength(WorkflowStep.NameMaxLength);
        builder.Ignore(a => a.Step);
        builder.Property(a => a.Body).HasMaxLength(RequestLimits.CommentMaxLength);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkTask>().WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.RequestId, a.OccurredAt });
        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => a.TaskId);
    }
}
