using Kyc.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the KYC module.
/// </summary>
public class KycDbContext : DbContext
{
    public const string DefaultSchema = "kyc";

    public KycDbContext(DbContextOptions<KycDbContext> options) : base(options)
    {
    }

    public DbSet<Applicant> Applicants => Set<Applicant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KycDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
