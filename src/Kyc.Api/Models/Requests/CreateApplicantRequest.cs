namespace Kyc.Api.Models.Requests;

/// <summary>
/// Request model for creating an applicant.
/// </summary>
public sealed record CreateApplicantRequest(
    string FirstName,
    string LastName,
    string Email,
    string? MiddleName = null,
    string? PhoneNumber = null,
    DateOnly? DateOfBirth = null,
    string? Nationality = null,
    AddressRequest? Address = null,
    string? ExternalReference = null,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// Request model for an address.
/// </summary>
public sealed record AddressRequest(
    string Line1,
    string? Line2,
    string City,
    string? State,
    string PostalCode,
    string Country
);
