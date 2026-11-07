using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using FlowOps.Application.Organization;
using FlowOps.Application.Tasks;
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
        services.AddScoped<AccessRules>();
        services.AddSingleton<CompanyCalendar>();
        services.AddScoped<RequestDetailsReader>();
        services.AddScoped<RequestSubmissionService>();
        services.AddScoped<RequestQueryService>();
        services.AddScoped<RequestCommentService>();
        services.AddScoped<TaskQueryService>();
        services.AddScoped<TaskDecisionService>();
        services.AddScoped<AuthService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<UserAdministrationService>();
        services.AddScoped<RoleChangeReader>();
        services.AddScoped<RoleChangeService>();
        return services;
    }
}
