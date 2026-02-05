namespace Kyc.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
