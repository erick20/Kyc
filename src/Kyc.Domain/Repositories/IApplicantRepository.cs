using Kyc.Domain.Entities;
using Kyc.Domain.ValueObjects;

namespace Kyc.Domain.Repositories;

/// <summary>
/// Repository interface for Applicant aggregate.
/// </summary>
public interface IApplicantRepository
{
    Task<Applicant?> GetByIdAsync(ApplicantId id, CancellationToken cancellationToken = default);
    Task<Applicant?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
    Task<Applicant?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(ApplicantId id, CancellationToken cancellationToken = default);
    Task AddAsync(Applicant applicant, CancellationToken cancellationToken = default);
    void Update(Applicant applicant);
    void Remove(Applicant applicant);
}
