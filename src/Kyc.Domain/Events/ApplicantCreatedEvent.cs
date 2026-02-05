using Kyc.Domain.Common;
using Kyc.Domain.ValueObjects;

namespace Kyc.Domain.Events;

/// <summary>
/// Event raised when a new applicant is created.
/// </summary>
public sealed record ApplicantCreatedEvent(
    ApplicantId ApplicantId,
    Email Email) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
