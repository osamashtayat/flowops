using FlowOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Infrastructure.Persistence.Configurations;

/// <summary>
/// Identity sets explicit PascalCase table and index names ("AspNetUsers", "RoleNameIndex") that the
/// snake_case convention does not override; DOMAIN_MODEL.md §6 requires snake_case throughout.
/// </summary>
internal static class IdentityTableNames
{
    public static void Apply(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_normalized_user_name");
            b.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_users_normalized_email");
        });
        builder.Entity<IdentityRole<Guid>>(b =>
        {
            b.ToTable("roles");
            b.HasIndex(r => r.NormalizedName).HasDatabaseName("ux_roles_normalized_name");
        });
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
