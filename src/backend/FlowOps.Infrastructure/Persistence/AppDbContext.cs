using FlowOps.Application.Common;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Infrastructure.Persistence;

/// <summary>The single EF Core context: Identity stores plus FlowOps aggregates (configurations added per task).</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        IdentityTableNames.Apply(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
