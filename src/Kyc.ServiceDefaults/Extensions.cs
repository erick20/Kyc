using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kyc.ServiceDefaults;

/// <summary>
/// Service defaults for KYC services - OpenTelemetry, health checks, etc.
/// </summary>
public static class Extensions
{
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services)
    {
        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        // Add health checks
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

        return services;
    }

    public static IServiceCollection AddServiceDefaults(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddServiceDefaults();

        // Add database health check
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "database", tags: new[] { "ready" });

        return services;
    }

    public static WebApplication MapServiceDefaults(this WebApplication app)
    {
        // Health check endpoints
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }
}
