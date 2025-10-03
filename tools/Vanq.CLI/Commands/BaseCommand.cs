using System.Diagnostics;
using Spectre.Console;
using Vanq.CLI.Configuration;
using Vanq.CLI.Models;
using Vanq.CLI.Services;
using Vanq.CLI.Telemetry;

namespace Vanq.CLI.Commands;

/// <summary>
/// Abstract base class for all CLI commands with common functionality.
/// </summary>
public abstract class BaseCommand
{
    protected VanqApiClient ApiClient { get; private set; } = null!;
    protected ITelemetryService TelemetryService { get; private set; } = null!;
    protected Models.Profile CurrentProfile { get; private set; } = null!;
    protected bool Verbose { get; set; }
    protected string OutputFormat { get; set; } = "table";
    protected bool NoColor { get; set; }
    protected bool Force { get; set; }

    /// <summary>
    /// Initializes the command with common configuration.
    /// </summary>
    protected async Task InitializeAsync(
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        Verbose = verbose;
        OutputFormat = outputFormat;
        NoColor = noColor;
        Force = force;

        // Disable colors if requested
        if (NoColor)
        {
            AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
        }

        // Load configuration
        var config = await ConfigManager.LoadConfigAsync();

        // Determine which profile to use
        var profileName = profileOverride ?? config.CurrentProfile;
        var profile = config.GetProfile(profileName);

        if (profile == null)
        {
            throw new InvalidOperationException(
                $"Profile '{profileName}' not found. Use 'vanq config add-profile' to create it.");
        }

        CurrentProfile = profile;

        // Use profile's output format if not overridden
        if (string.IsNullOrEmpty(OutputFormat) || OutputFormat == "table")
        {
            OutputFormat = profile.OutputFormat ?? "table";
        }

        // Initialize API client
        ApiClient = new VanqApiClient(profile.ApiEndpoint, profile.Name);
        await ApiClient.InitializeAsync();

        // Initialize telemetry service
        TelemetryService = CreateTelemetryService(config);

        if (Verbose)
        {
            LogVerbose($"Profile: {profile.Name}");
            LogVerbose($"API Endpoint: {profile.ApiEndpoint}");
            LogVerbose($"Output Format: {OutputFormat}");
            LogVerbose($"Authenticated: {ApiClient.IsAuthenticated}");
        }
    }

    /// <summary>
    /// Executes the command with telemetry tracking and error handling.
    /// </summary>
    protected async Task<int> ExecuteWithTrackingAsync(
        string commandName,
        Func<Task<int>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        string? errorType = null;

        try
        {
            var exitCode = await action();
            success = exitCode == 0;
            return exitCode;
        }
        catch (Exception ex)
        {
            errorType = ex.GetType().Name;
            success = false;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Track command execution in telemetry
            var metadata = new Dictionary<string, string>
            {
                ["OutputFormat"] = OutputFormat,
                ["Verbose"] = Verbose.ToString()
            };

            await TelemetryService.TrackCommandAsync(
                commandName,
                success,
                stopwatch.Elapsed,
                errorType,
                metadata);

            if (Verbose)
            {
                LogVerbose($"Command completed in {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }

    /// <summary>
    /// Ensures the user is authenticated before executing a command.
    /// </summary>
    protected void RequireAuthentication()
    {
        if (!ApiClient.IsAuthenticated)
        {
            throw new InvalidOperationException(
                "Authentication required. Please run 'vanq login' first.");
        }
    }

    /// <summary>
    /// Prompts for confirmation before executing a destructive action.
    /// </summary>
    protected bool ConfirmAction(string message, bool defaultValue = false)
    {
        if (Force)
        {
            LogVerbose("Confirmation bypassed with --force flag");
            return true;
        }

        return AnsiConsole.Confirm(message, defaultValue);
    }

    /// <summary>
    /// Logs a verbose message if verbose mode is enabled.
    /// </summary>
    protected void LogVerbose(string message)
    {
        if (Verbose)
        {
            AnsiConsole.MarkupLine($"[dim][DEBUG][/] {Markup.Escape(message)}");
        }
    }

    /// <summary>
    /// Displays an error message.
    /// </summary>
    protected void LogError(string message, Exception? ex = null)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");

        if (ex != null && Verbose)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    /// <summary>
    /// Displays a success message.
    /// </summary>
    protected void LogSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Displays a warning message.
    /// </summary>
    protected void LogWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Displays an info message.
    /// </summary>
    protected void LogInfo(string message)
    {
        AnsiConsole.MarkupLine($"[cyan]ℹ[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Formats and displays output based on the selected format.
    /// </summary>
    protected void DisplayOutput<T>(T data) where T : class
    {
        switch (OutputFormat.ToLowerInvariant())
        {
            case "json":
                DisplayJson(data);
                break;
            case "table":
                DisplayTable(data);
                break;
            case "csv":
                DisplayCsv(data);
                break;
            default:
                throw new ArgumentException($"Unsupported output format: {OutputFormat}");
        }
    }

    /// <summary>
    /// Displays data in JSON format.
    /// </summary>
    protected virtual void DisplayJson<T>(T data) where T : class
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        AnsiConsole.WriteLine(json);
    }

    /// <summary>
    /// Displays data in table format (to be overridden by subclasses).
    /// </summary>
    protected virtual void DisplayTable<T>(T data) where T : class
    {
        // Default implementation - subclasses should override for specific data types
        DisplayJson(data);
        LogWarning("Table format not implemented for this command, showing JSON instead");
    }

    /// <summary>
    /// Displays data in CSV format (to be overridden by subclasses).
    /// </summary>
    protected virtual void DisplayCsv<T>(T data) where T : class
    {
        // Default implementation - subclasses should override for specific data types
        DisplayJson(data);
        LogWarning("CSV format not implemented for this command, showing JSON instead");
    }

    /// <summary>
    /// Creates a telemetry service based on configuration.
    /// </summary>
    private static ITelemetryService CreateTelemetryService(CliConfig config)
    {
        if (config.Telemetry == null || !config.Telemetry.Enabled || config.Telemetry.ConsentGiven != true)
        {
            return new NoOpTelemetryService();
        }

        return new TelemetryService(config.Telemetry);
    }

    /// <summary>
    /// Handles HTTP response errors with user-friendly messages.
    /// </summary>
    protected async Task<int> HandleHttpErrorAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.Unauthorized:
                LogError("Authentication failed. Please run 'vanq login' to authenticate.");
                return 2; // Exit code for authentication failure

            case System.Net.HttpStatusCode.Forbidden:
                LogError("Permission denied. You don't have the required permissions for this operation.");
                return 3; // Exit code for permission denied

            case System.Net.HttpStatusCode.NotFound:
                LogError("Resource not found.");
                return 4; // Exit code for not found

            case System.Net.HttpStatusCode.BadRequest:
                var errorContent = await response.Content.ReadAsStringAsync();
                LogError($"Invalid request: {errorContent}");
                return 5; // Exit code for validation failure

            default:
                var content = await response.Content.ReadAsStringAsync();
                LogError($"HTTP {statusCode}: {response.ReasonPhrase}");
                if (Verbose && !string.IsNullOrWhiteSpace(content))
                {
                    LogVerbose($"Response body: {content}");
                }
                return 1; // Generic error
        }
    }
}
