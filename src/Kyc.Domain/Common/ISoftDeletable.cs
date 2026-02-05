namespace Kyc.Domain.Common;

/// <summary>
/// Interface for entities that support soft deletion.
/// </summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
    bool IsDeleted => DeletedAt.HasValue;
}
