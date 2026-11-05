using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and Identity (stores, sign-in, bootstrap admin). The connection string comes from configuration
    /// (<c>ConnectionStrings__FlowOps</c> env var or user-secrets), never from committed appsettings.
    /// It is read when a context is first created, so the app starts (e.g. /api/health) without one.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(PersistenceOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'FlowOps' is not configured. Set ConnectionStrings__FlowOps or use dotnet user-secrets.");
            }

            PersistenceOptions.Configure(options, connectionString);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IRequestNumberSequence, RequestNumberSequence>();
        services.AddFlowOpsIdentity(configuration);
        return services;
    }
}
