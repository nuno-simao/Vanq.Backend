namespace Vanq.CLI.Models;

/// <summary>
/// Telemetry configuration settings.
/// </summary>
public class TelemetrySettings
{
    public bool Enabled { get; set; } = true; // Opt-out by default
    public bool? ConsentGiven { get; set; }
    public DateTime? ConsentDate { get; set; }
    public string? AnonymousId { get; set; }
    public string Endpoint { get; set; } = "http://localhost:5000/api/telemetry/cli";

    public TelemetrySettings() { }
}
