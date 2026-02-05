using MediatR;

namespace Kyc.Application.Applicants.Commands.CreateApplicant;

/// <summary>
/// Command to create a new applicant.
/// </summary>
public sealed record CreateApplicantCommand(
    string FirstName,
    string LastName,
    string Email,
    string? MiddleName = null,
    string? PhoneNumber = null,
    DateOnly? DateOfBirth = null,
    string? Nationality = null,
    CreateApplicantAddressDto? Address = null,
    string? ExternalReference = null,
    Dictionary<string, string>? Metadata = null
) : IRequest<CreateApplicantResult>;

/// <summary>
/// Address DTO for creating an applicant.
/// </summary>
public sealed record CreateApplicantAddressDto(
    string Line1,
    string? Line2,
    string City,
    string? State,
    string PostalCode,
    string Country
);

/// <summary>
/// Result of creating an applicant.
/// </summary>
public sealed record CreateApplicantResult(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime CreatedAt
);
