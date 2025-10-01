namespace Vanq.Infrastructure.Logging;

public sealed class LoggingOptions
{
    public string MinimumLevel { get; init; } = "Information";
    public string[] MaskedFields { get; init; } = [];
    public bool ConsoleJson { get; init; } = true;
    public string? FilePath { get; init; }
    public bool EnableRequestLogging { get; init; } = true;
    public string SensitiveValuePlaceholder { get; init; } = "***";
}
