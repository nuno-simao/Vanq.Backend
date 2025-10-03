using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;

namespace Vanq.CLI.Commands.Health;

/// <summary>
/// Checks API health status.
/// </summary>
public class HealthCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("health", "Check API health status");

        command.SetHandler(async (context) =>
        {
            var healthCommand = new HealthCommand();
            var exitCode = await healthCommand.ExecuteAsync(
                GlobalOptionsHelper.GetVerbose(context),
                GlobalOptionsHelper.GetOutputFormat(context),
                GlobalOptionsHelper.GetProfile(context),
                GlobalOptionsHelper.GetNoColor(context),
                GlobalOptionsHelper.GetForce(context)
            );
            context.ExitCode = exitCode;
        });

        return command;
    }

    private async Task<int> ExecuteAsync(
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("health", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            LogVerbose("Checking API health...");

            try
            {
                var response = await ApiClient.GetAsync("/health");

                if (!response.IsSuccessStatusCode)
                {
                    LogError($"API health check failed: {response.StatusCode}");
                    return 1;
                }

                var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

                if (health == null)
                {
                    LogError("Invalid health response");
                    return 1;
                }

                DisplayOutput(health);

                // Return exit code based on health status
                return health.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            }
            catch (Exception ex)
            {
                LogError("Failed to check API health", ex);
                return 1;
            }
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is HealthResponse health)
        {
            var statusColor = health.Status.ToLowerInvariant() switch
            {
                "healthy" => "green",
                "degraded" => "yellow",
                "unhealthy" => "red",
                _ => "white"
            };

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("Status", $"[{statusColor} bold]{health.Status}[/]");
            table.AddRow("Total Duration", $"{health.TotalDuration:F2}ms");

            AnsiConsole.Write(table);

            // Display individual checks if present
            if (health.Entries != null && health.Entries.Count > 0)
            {
                AnsiConsole.WriteLine();
                var checksTable = new Table();
                checksTable.Border(TableBorder.Rounded);
                checksTable.AddColumn("Check");
                checksTable.AddColumn("Status");
                checksTable.AddColumn("Duration");
                checksTable.AddColumn("Description");

                foreach (var entry in health.Entries)
                {
                    var checkColor = entry.Value.Status.ToLowerInvariant() switch
                    {
                        "healthy" => "green",
                        "degraded" => "yellow",
                        "unhealthy" => "red",
                        _ => "white"
                    };

                    checksTable.AddRow(
                        entry.Key,
                        $"[{checkColor}]{entry.Value.Status}[/]",
                        $"{entry.Value.Duration:F2}ms",
                        entry.Value.Description ?? "-");
                }

                AnsiConsole.Write(checksTable);
            }
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private record HealthResponse(
        string Status,
        double TotalDuration,
        Dictionary<string, HealthEntry>? Entries);

    private record HealthEntry(
        string Status,
        double Duration,
        string? Description);
}
