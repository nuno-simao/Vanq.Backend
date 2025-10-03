namespace Vanq.CLI.Telemetry;

/// <summary>
/// Anonymous telemetry event.
/// </summary>
public record TelemetryEvent
{
    public string AnonymousId { get; init; } = null!;
    public string SessionId { get; init; } = null!;
    public string CommandName { get; init; } = null!;
    public bool Success { get; init; }
    public int DurationMs { get; init; }
    public string? ErrorType { get; init; }
    public string CliVersion { get; init; } = null!;
    public string OsPlatform { get; init; } = null!;
    public string OsVersion { get; init; } = null!;
    public string DotNetVersion { get; init; } = null!;
    public DateTime Timestamp { get; init; }
    public string? OutputFormat { get; init; }
    public bool VerboseMode { get; init; }
}
