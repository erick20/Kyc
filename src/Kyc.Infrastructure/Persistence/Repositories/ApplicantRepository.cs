using Kyc.Domain.Entities;
using Kyc.Domain.Repositories;
using Kyc.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the Applicant repository.
/// </summary>
internal sealed class ApplicantRepository : IApplicantRepository
{
    private readonly KycDbContext _context;

    public ApplicantRepository(KycDbContext context)
    {
        _context = context;
    }

    public async Task<Applicant?> GetByIdAsync(ApplicantId id, CancellationToken cancellationToken = default)
    {
        return await _context.Applicants
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Applicant?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _context.Applicants
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);
    }

    public async Task<Applicant?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default)
    {
        return await _context.Applicants
            .FirstOrDefaultAsync(a => a.ExternalReference == externalReference, cancellationToken);
    }

    public async Task<bool> ExistsAsync(ApplicantId id, CancellationToken cancellationToken = default)
    {
        return await _context.Applicants
            .AnyAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAsync(Applicant applicant, CancellationToken cancellationToken = default)
    {
        await _context.Applicants.AddAsync(applicant, cancellationToken);
    }

    public void Update(Applicant applicant)
    {
        _context.Applicants.Update(applicant);
    }

    public void Remove(Applicant applicant)
    {
        _context.Applicants.Update(applicant);
    }
}
