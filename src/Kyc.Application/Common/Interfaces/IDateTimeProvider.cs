namespace Kyc.Application.Common.Interfaces;

/// <summary>
/// Abstraction for DateTime operations to enable testing.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}
