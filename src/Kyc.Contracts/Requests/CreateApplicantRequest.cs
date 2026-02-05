namespace Kyc.Contracts.Requests;

/// <summary>
/// Contract for creating an applicant.
/// Shared between API and NuGet package consumers.
/// </summary>
public sealed record CreateApplicantRequest(
    string FirstName,
    string LastName,
    string Email,
    string? MiddleName = null,
    string? PhoneNumber = null,
    DateOnly? DateOfBirth = null,
    string? Nationality = null,
    AddressDto? Address = null,
    string? ExternalReference = null,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// Address DTO for contracts.
/// </summary>
public sealed record AddressDto(
    string Line1,
    string? Line2,
    string City,
    string? State,
    string PostalCode,
    string Country
);
