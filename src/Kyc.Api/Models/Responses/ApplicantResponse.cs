namespace Kyc.Api.Models.Responses;

/// <summary>
/// Response model for an applicant.
/// </summary>
public sealed record ApplicantResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime CreatedAt
);
