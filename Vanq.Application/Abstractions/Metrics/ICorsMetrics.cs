namespace Vanq.Application.Abstractions.Metrics;

/// <summary>
/// Placeholder interface for CORS metrics
/// Will be implemented in SPEC-0010 (Metrics/Telemetry)
/// </summary>
public interface ICorsMetrics
{
    /// <summary>
    /// Increments counter for blocked CORS requests
    /// </summary>
    /// <param name="origin">Origin that was blocked</param>
    /// <param name="path">Request path</param>
    void IncrementBlocked(string origin, string path);

    /// <summary>
    /// Increments counter for allowed CORS requests
    /// </summary>
    /// <param name="origin">Origin that was allowed</param>
    /// <param name="path">Request path</param>
    void IncrementAllowed(string origin, string path);

    /// <summary>
    /// Records preflight request duration
    /// </summary>
    /// <param name="origin">Origin of the request</param>
    /// <param name="durationMs">Duration in milliseconds</param>
    void RecordPreflightDuration(string origin, long durationMs);
}
