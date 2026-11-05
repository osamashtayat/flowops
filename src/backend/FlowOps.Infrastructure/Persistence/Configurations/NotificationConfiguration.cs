using FlowOps.Domain.Notifications;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>DOMAIN_MODEL.md §2.7, §6. One row per recipient; written in the same transaction as the decision (ADR-0009).</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", t =>
            t.HasCheckConstraint("ck_notifications_type", CheckConstraints.InEnum<NotificationType>("type")));
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(n => n.Body).HasMaxLength(Notification.BodyMaxLength);
        builder.Ignore(n => n.IsRead);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Request>().WithMany().HasForeignKey(n => n.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkTask>().WithMany().HasForeignKey(n => n.TaskId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.RecipientId, n.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(n => n.RecipientId).HasFilter("read_at IS NULL").HasDatabaseName("ix_notifications_unread_recipient_id");
        builder.HasIndex(n => n.RequestId);
        builder.HasIndex(n => n.TaskId);
    }
}
