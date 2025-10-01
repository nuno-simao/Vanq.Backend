using Vanq.Application.Abstractions.Time;

namespace Vanq.Infrastructure;

/// <summary>
/// Provides extension methods for IDateTimeProvider.
/// </summary>
internal static class DateTimeProviderExtensions
{
    /// <summary>
    /// Gets the current UTC time as a DateTimeOffset with UTC kind explicitly set.
    /// </summary>
    /// <param name="clock">The date time provider.</param>
    /// <returns>A DateTimeOffset representing the current UTC time.</returns>
    internal static DateTimeOffset GetUtcDateTimeOffset(this IDateTimeProvider clock)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));
    }

    /// <summary>
    /// Gets the current UTC time as a DateTime with UTC kind explicitly set.
    /// </summary>
    /// <param name="clock">The date time provider.</param>
    /// <returns>A DateTime with UTC kind.</returns>
    internal static DateTime GetUtcDateTime(this IDateTimeProvider clock)
    {
        return DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);
    }
}
