namespace Kyc.Domain.Repositories;

/// <summary>
/// Unit of work interface for coordinating persistence.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
