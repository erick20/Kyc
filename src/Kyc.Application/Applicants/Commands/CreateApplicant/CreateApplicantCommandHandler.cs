using Kyc.Application.Common.Exceptions;
using Kyc.Domain.Entities;
using Kyc.Domain.Repositories;
using Kyc.Domain.ValueObjects;
using MediatR;

namespace Kyc.Application.Applicants.Commands.CreateApplicant;

/// <summary>
/// Handler for CreateApplicantCommand.
/// </summary>
public sealed class CreateApplicantCommandHandler : IRequestHandler<CreateApplicantCommand, CreateApplicantResult>
{
    private readonly IApplicantRepository _applicantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateApplicantCommandHandler(
        IApplicantRepository applicantRepository,
        IUnitOfWork unitOfWork)
    {
        _applicantRepository = applicantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateApplicantResult> Handle(
        CreateApplicantCommand request,
        CancellationToken cancellationToken)
    {
        // Create email value object
        var email = Email.Create(request.Email);

        // Check for duplicate email
        var existingApplicant = await _applicantRepository.GetByEmailAsync(email, cancellationToken);
        if (existingApplicant is not null)
        {
            throw new ConflictException("Applicant", request.Email);
        }

        // Check for duplicate external reference if provided
        if (!string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            var existingByRef = await _applicantRepository.GetByExternalReferenceAsync(
                request.ExternalReference,
                cancellationToken);

            if (existingByRef is not null)
            {
                throw new ConflictException("Applicant", request.ExternalReference);
            }
        }

        // Create address value object if provided
        Address? address = null;
        if (request.Address is not null)
        {
            address = Address.Create(
                request.Address.Line1,
                request.Address.Line2,
                request.Address.City,
                request.Address.State,
                request.Address.PostalCode,
                request.Address.Country);
        }

        // Create the applicant entity
        var applicant = Applicant.Create(
            firstName: request.FirstName,
            lastName: request.LastName,
            email: email,
            middleName: request.MiddleName,
            phoneNumber: request.PhoneNumber,
            dateOfBirth: request.DateOfBirth,
            nationality: request.Nationality,
            address: address,
            externalReference: request.ExternalReference,
            metadata: request.Metadata);

        // Persist
        await _applicantRepository.AddAsync(applicant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return result
        return new CreateApplicantResult(
            Id: applicant.Id.Value,
            FirstName: applicant.FirstName,
            LastName: applicant.LastName,
            Email: applicant.Email.Value,
            CreatedAt: applicant.CreatedAt);
    }
}
