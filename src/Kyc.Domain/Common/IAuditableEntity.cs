namespace Kyc.Domain.Common;

/// <summary>
/// Interface for entities that track creation and modification timestamps.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}
