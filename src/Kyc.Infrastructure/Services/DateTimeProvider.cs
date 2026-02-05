using Kyc.Application.Common.Interfaces;

namespace Kyc.Infrastructure.Services;

/// <summary>
/// Default implementation of IDateTimeProvider.
/// </summary>
internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
