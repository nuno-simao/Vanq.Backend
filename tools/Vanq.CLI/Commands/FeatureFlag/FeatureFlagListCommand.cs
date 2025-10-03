using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;

namespace Vanq.CLI.Commands.FeatureFlag;

/// <summary>
/// Lists all feature flags for the current environment.
/// </summary>
public class FeatureFlagListCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("list", "List all feature flags");

        var allEnvironmentsOption = new Option<bool>(
            aliases: ["--all-environments", "-a"],
            description: "Show flags for all environments");

        command.AddOption(allEnvironmentsOption);

        command.SetHandler(async (context) =>
        {
            var allEnvironments = context.ParseResult.GetValueForOption(allEnvironmentsOption);

            var listCommand = new FeatureFlagListCommand();
            var exitCode = await listCommand.ExecuteAsync(
                allEnvironments,
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
        bool allEnvironments,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("feature-flag.list", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            var endpoint = allEnvironments
                ? "/api/admin/feature-flags"
                : "/api/admin/feature-flags/current";

            LogVerbose($"Fetching feature flags from {endpoint}...");

            try
            {
                var response = await ApiClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var flags = await response.Content.ReadFromJsonAsync<List<FeatureFlagDto>>();

                if (flags == null || flags.Count == 0)
                {
                    LogWarning("No feature flags found");
                    return 0;
                }

                DisplayOutput(new FeatureFlagList { Flags = flags });

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Failed to fetch feature flags", ex);
                return 1;
            }
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is FeatureFlagList flagList)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Key");
            table.AddColumn("Environment");
            table.AddColumn("Enabled");
            table.AddColumn("Description");
            table.AddColumn("Updated");

            foreach (var flag in flagList.Flags.OrderBy(f => f.Key).ThenBy(f => f.Environment))
            {
                var enabledMarker = flag.IsEnabled ? "[green]✓[/]" : "[red]✗[/]";
                var key = flag.IsEnabled ? $"[green]{flag.Key}[/]" : $"[dim]{flag.Key}[/]";

                table.AddRow(
                    key,
                    flag.Environment,
                    enabledMarker,
                    flag.Description ?? "-",
                    flag.UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-");
            }

            AnsiConsole.Write(table);
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private class FeatureFlagList
    {
        public List<FeatureFlagDto> Flags { get; set; } = [];
    }

    private record FeatureFlagDto(
        string Key,
        string Environment,
        bool IsEnabled,
        string? Description,
        DateTime? UpdatedAt);
}
