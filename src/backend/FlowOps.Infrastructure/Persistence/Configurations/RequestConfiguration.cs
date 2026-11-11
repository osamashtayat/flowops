using FlowOps.Domain.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using FlowOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>The <c>Request</c> aggregate root and its 1:1 details (DOMAIN_MODEL.md §2.3, §6; ADR-0006 for <c>xmin</c>).</summary>
internal sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    /// <summary>Shadow key of the 1:1 details tables: their primary key is also the FK to <c>requests</c>.</summary>
    private const string DetailsKey = "RequestId";

    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("requests", t =>
        {
            t.HasCheckConstraint("ck_requests_type", CheckConstraints.InEnum<RequestType>("type"));
            t.HasCheckConstraint("ck_requests_status", CheckConstraints.InEnum<RequestStatus>("status"));
            // A closed request has no current step (DOMAIN_MODEL.md §7.4): closed is a status, not a step.
            t.HasCheckConstraint("ck_requests_step_iff_open",
                "(status IN ('Completed', 'Rejected')) = (step_key IS NULL) AND (step_key IS NULL) = (step_name IS NULL)");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Number)
            .HasConversion(n => n.Value, s => RequestNumber.Parse(s))
            .HasMaxLength(RequestNumber.MaxLength);
        builder.HasIndex(r => r.Number).IsUnique().HasDatabaseName("ux_requests_number");
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        builder.Property(r => r.StepKey).HasMaxLength(WorkflowStep.KeyMaxLength);
        builder.Property(r => r.StepName).HasMaxLength(WorkflowStep.NameMaxLength);
        builder.Ignore(r => r.Step);
        builder.Property(r => r.Version).IsRowVersion();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowVersion>().WithMany().HasForeignKey(r => r.WorkflowVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.RequesterId, r.SubmittedAt }).IsDescending(false, true);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.DepartmentId);
        builder.HasIndex(r => r.WorkflowVersionId);

        builder.OwnsOne(r => r.Purchase, ConfigurePurchase);
        builder.OwnsOne(r => r.Leave, ConfigureLeave);

        builder.HasMany(r => r.Tasks).WithOne().HasForeignKey(t => t.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(r => r.Activities).WithOne().HasForeignKey(a => a.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Activities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.IsClosed);
        builder.Ignore(r => r.OpenTask);
    }

    private static void ConfigurePurchase(OwnedNavigationBuilder<Request, PurchaseDetails> purchase)
    {
        purchase.ToTable("purchase_request_details", t =>
        {
            t.HasCheckConstraint("ck_purchase_request_details_currency", CheckConstraints.In("estimated_cost_currency", SupportedCurrencies.All));
            t.HasCheckConstraint(
                "ck_purchase_request_details_amount",
                FormattableString.Invariant($"estimated_cost_amount > 0 AND estimated_cost_amount <= {Money.MaxAmount}"));
        });
        purchase.WithOwner().HasForeignKey(DetailsKey);
        purchase.HasKey(DetailsKey);
        purchase.Property<Guid>(DetailsKey).HasColumnName("request_id");
        purchase.Property(p => p.ItemName).HasMaxLength(RequestLimits.ItemNameMaxLength);
        purchase.Property(p => p.Description).HasMaxLength(RequestLimits.DescriptionMaxLength);
        purchase.Property(p => p.Justification).HasMaxLength(RequestLimits.JustificationMaxLength);
        purchase.OwnsOne(p => p.EstimatedCost, money =>
        {
            money.Property(m => m.Amount).HasColumnName("estimated_cost_amount").HasPrecision(18, Money.Scale);
            money.Property(m => m.Currency).HasColumnName("estimated_cost_currency").HasColumnType("char(3)");
        });
        purchase.Navigation(p => p.EstimatedCost).IsRequired();
    }

    private static void ConfigureLeave(OwnedNavigationBuilder<Request, LeaveDetails> leave)
    {
        leave.ToTable("leave_request_details", t =>
        {
            t.HasCheckConstraint("ck_leave_request_details_leave_type", CheckConstraints.InEnum<LeaveType>("leave_type"));
            t.HasCheckConstraint("ck_leave_request_details_dates", "end_date >= start_date");
        });
        leave.WithOwner().HasForeignKey(DetailsKey);
        leave.HasKey(DetailsKey);
        leave.Property<Guid>(DetailsKey).HasColumnName("request_id");
        leave.Property(l => l.LeaveType).HasConversion<string>().HasMaxLength(CheckConstraints.EnumMaxLength);
        leave.Property(l => l.Reason).HasMaxLength(RequestLimits.LeaveReasonMaxLength);
        leave.Ignore(l => l.Days);
    }
}
