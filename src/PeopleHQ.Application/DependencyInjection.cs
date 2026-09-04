using Microsoft.Extensions.DependencyInjection;

namespace PeopleHQ.Application;

/// <summary>
/// MediatR registration lives in PeopleHQ.Infrastructure.DependencyInjection
/// (not here), because handlers are implemented in Infrastructure — Application
/// only defines the command/query contracts — and MediatR needs both assemblies
/// scanned from one place that can see both. This method exists as the
/// Application-only registration point (e.g. FluentValidation validators, once added).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
