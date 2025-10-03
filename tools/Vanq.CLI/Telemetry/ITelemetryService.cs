namespace Vanq.CLI.Telemetry;

/// <summary>
/// Interface for telemetry service.
/// </summary>
public interface ITelemetryService
{
    Task TrackCommandAsync(
        string commandName,
        bool success,
        TimeSpan duration,
        string? errorType = null,
        Dictionary<string, string>? metadata = null);

    Task FlushAsync();
    bool IsEnabled { get; }
}
