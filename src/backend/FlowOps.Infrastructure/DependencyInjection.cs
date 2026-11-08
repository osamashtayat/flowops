using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence;
using FlowOps.Infrastructure.Realtime;
using FlowOps.Infrastructure.Seeding;
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
        // Clients send nothing but the handshake and pings; detailed errors stay off (no internals to clients).
        services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024;
            options.EnableDetailedErrors = false;
        });
        services.AddOptions<RealtimeConnectionOptions>()
            .Bind(configuration.GetSection(RealtimeConnectionOptions.SectionName))
            .Validate(o => o.MaxConnectionsPerUser > 0, "FlowOps:Realtime:MaxConnectionsPerUser must be positive.")
            .ValidateOnStart();
        services.AddSingleton<HubConnectionRegistry>();
        services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();
        services.AddScoped<DemoDataSeeder>(); // runs only with --seed-demo, and only in Development (BE-16)
        return services;
    }
}
