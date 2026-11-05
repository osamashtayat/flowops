using FlowOps.Domain.Organization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>
/// The fixed role set (<see cref="Roles.All"/>) is reference data, seeded by migration with stable ids so
/// every environment agrees and test resets can keep the table.
/// </summary>
internal sealed class RoleSeedConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    private static readonly Dictionary<string, Guid> RoleIds = new(StringComparer.Ordinal)
    {
        [Roles.Administrator] = new("0193a000-0000-7000-8000-000000000001"),
        [Roles.Employee] = new("0193a000-0000-7000-8000-000000000002"),
        [Roles.FinanceOfficer] = new("0193a000-0000-7000-8000-000000000003"),
        [Roles.HrOfficer] = new("0193a000-0000-7000-8000-000000000004"),
        [Roles.ProcurementOfficer] = new("0193a000-0000-7000-8000-000000000005"),
    };

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) =>
        builder.HasData(Roles.All.Select(name => new IdentityRole<Guid>
        {
            Id = RoleIds[name],
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = RoleIds[name].ToString(),
        }));
}
