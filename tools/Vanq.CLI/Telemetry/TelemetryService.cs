using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Vanq.CLI.Models;

namespace Vanq.CLI.Telemetry;

/// <summary>
/// Telemetry service implementation with opt-out support.
/// </summary>
public class TelemetryService : ITelemetryService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TelemetrySettings _settings;
    private readonly string _anonymousId;
    private readonly string _sessionId;
    private readonly ConcurrentQueue<TelemetryEvent> _eventQueue;
    private readonly Timer _flushTimer;

    public TelemetryService(TelemetrySettings settings)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _settings = settings;
        _anonymousId = settings.AnonymousId ?? Guid.NewGuid().ToString();
        _sessionId = Guid.NewGuid().ToString();
        _eventQueue = new ConcurrentQueue<TelemetryEvent>();

        // Flush every 30 seconds
        _flushTimer = new Timer(async _ => await FlushAsync(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task TrackCommandAsync(
        string commandName,
        bool success,
        TimeSpan duration,
        string? errorType = null,
        Dictionary<string, string>? metadata = null)
    {
        if (!_settings.Enabled || _settings.ConsentGiven != true)
            return;

        var telemetryEvent = new TelemetryEvent
        {
            AnonymousId = _anonymousId,
            SessionId = _sessionId,
            CommandName = commandName,
            Success = success,
            DurationMs = (int)duration.TotalMilliseconds,
            ErrorType = errorType,
            CliVersion = GetCliVersion(),
            OsPlatform = GetOsPlatform(),
            OsVersion = Environment.OSVersion.VersionString,
            DotNetVersion = Environment.Version.ToString(),
            Timestamp = DateTime.UtcNow,
            OutputFormat = metadata?.GetValueOrDefault("OutputFormat"),
            VerboseMode = metadata?.GetValueOrDefault("Verbose") == "true"
        };

        _eventQueue.Enqueue(telemetryEvent);

        if (_eventQueue.Count > 10)
            await FlushAsync();
    }

    public async Task FlushAsync()
    {
        if (_eventQueue.IsEmpty)
            return;

        var events = new List<TelemetryEvent>();
        while (_eventQueue.TryDequeue(out var evt))
            events.Add(evt);

        try
        {
            await _httpClient.PostAsJsonAsync(_settings.Endpoint, events);
        }
        catch
        {
            // Silently ignore telemetry errors
        }
    }

    public bool IsEnabled => _settings.Enabled && _settings.ConsentGiven == true;

    private static string GetCliVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.1.0";
    }

    private static string GetOsPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushAsync().Wait();
        _httpClient?.Dispose();
    }
}

/// <summary>
/// No-op telemetry service (when opted out).
/// </summary>
public class NoOpTelemetryService : ITelemetryService
{
    public Task TrackCommandAsync(string commandName, bool success, TimeSpan duration,
        string? errorType = null, Dictionary<string, string>? metadata = null)
        => Task.CompletedTask;

    public Task FlushAsync() => Task.CompletedTask;
    public bool IsEnabled => false;
}
