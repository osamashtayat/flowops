using Microsoft.EntityFrameworkCore;

namespace FlowOps.Infrastructure.Persistence;

/// <summary>The one place the provider and naming conventions are configured, for runtime and design time alike.</summary>
internal static class PersistenceOptions
{
    public const string ConnectionStringName = "FlowOps";

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();
}
