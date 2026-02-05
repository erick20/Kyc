using Kyc.Domain.Repositories;

namespace Kyc.Infrastructure.Persistence;

/// <summary>
/// Unit of work implementation using EF Core.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly KycDbContext _context;

    public UnitOfWork(KycDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
