using Kyc.Application.Common.Interfaces;
using Kyc.Domain.Repositories;
using Kyc.Infrastructure.Persistence;
using Kyc.Infrastructure.Persistence.Repositories;
using Kyc.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Infrastructure;

/// <summary>
/// Dependency injection extensions for the Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // Register DbContext
        services.AddDbContext<KycDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(KycDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
        });

        // Register repositories
        services.AddScoped<IApplicantRepository, ApplicantRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
