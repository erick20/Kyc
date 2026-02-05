namespace Kyc.Contracts.Responses;

/// <summary>
/// Contract for applicant response.
/// Shared between API and NuGet package consumers.
/// </summary>
public sealed record ApplicantResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime CreatedAt
);
