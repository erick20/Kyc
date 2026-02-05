namespace Kyc.Contracts.Enums;

/// <summary>
/// Status of an applicant in the KYC process.
/// </summary>
public enum ApplicantStatus
{
    Pending = 0,
    InProgress = 1,
    Approved = 2,
    Rejected = 3,
    RequiresReview = 4
}
