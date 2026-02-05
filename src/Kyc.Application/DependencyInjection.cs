using FluentValidation;
using Kyc.Application.Common.Behaviors;
using Kyc.Application.Common.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Application;

/// <summary>
/// Dependency injection extensions for the Application layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Register custom mediator and handlers
        services.AddMediator(assembly);

        // Register validators
        services.AddValidatorsFromAssembly(assembly);

        // Register pipeline behaviors (order matters - first registered executes first)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
