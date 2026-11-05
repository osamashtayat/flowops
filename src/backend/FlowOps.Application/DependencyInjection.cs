using FlowOps.Application.Organization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowOps.Application;

public static class DependencyInjection
{
    /// <summary>Registers Application services. Use-case services are concrete classes, registered directly.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // All time comes from TimeProvider so tests can substitute FakeTimeProvider (TESTING.md §2).
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuthService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<UserAdministrationService>();
        return services;
    }
}
