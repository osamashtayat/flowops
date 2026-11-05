using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>DOMAIN_MODEL.md §2.1. Case-insensitive name uniqueness is a functional index created in the migration.</summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public const string CodeIndex = "ux_departments_code";

    /// <summary>Functional index on <c>lower(name)</c>, created by raw SQL in the AuthAndOrganization migration.</summary>
    public const string NameIndex = "ux_departments_name_ci";

    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.Code).HasMaxLength(Department.CodeMaxLength).IsRequired();
        builder.HasIndex(d => d.Code).IsUnique().HasDatabaseName(CodeIndex);
    }
}
