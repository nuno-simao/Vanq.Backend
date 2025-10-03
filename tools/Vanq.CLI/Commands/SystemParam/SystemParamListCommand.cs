using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;

namespace Vanq.CLI.Commands.SystemParam;

/// <summary>
/// Lists all system parameters.
/// </summary>
public class SystemParamListCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("list", "List all system parameters");

        var categoryOption = new Option<string>(
            aliases: ["--category", "-c"],
            description: "Filter by category");

        command.AddOption(categoryOption);

        command.SetHandler(async (context) =>
        {
            var category = context.ParseResult.GetValueForOption(categoryOption);

            var listCommand = new SystemParamListCommand();
            var exitCode = await listCommand.ExecuteAsync(
                category,
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
        string? category,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("system-param.list", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            var endpoint = "/api/admin/system-params";
            if (!string.IsNullOrEmpty(category))
            {
                endpoint += $"?category={Uri.EscapeDataString(category)}";
            }

            LogVerbose($"Fetching system parameters from {endpoint}...");

            try
            {
                var response = await ApiClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var parameters = await response.Content.ReadFromJsonAsync<List<SystemParamDto>>();

                if (parameters == null || parameters.Count == 0)
                {
                    LogWarning("No system parameters found");
                    return 0;
                }

                DisplayOutput(new SystemParamList { Parameters = parameters });

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Failed to fetch system parameters", ex);
                return 1;
            }
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is SystemParamList paramList)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Key");
            table.AddColumn("Category");
            table.AddColumn("Value");
            table.AddColumn("Data Type");
            table.AddColumn("Description");
            table.AddColumn("System Managed");

            foreach (var param in paramList.Parameters.OrderBy(p => p.Category).ThenBy(p => p.Key))
            {
                var systemManaged = param.IsSystemManaged ? "[yellow]✓[/]" : "";
                var key = param.IsSystemManaged ? $"[yellow]{param.Key}[/]" : param.Key;

                // Truncate long values
                var value = param.Value.Length > 50
                    ? param.Value[..47] + "..."
                    : param.Value;

                table.AddRow(
                    key,
                    param.Category ?? "-",
                    value,
                    param.DataType,
                    param.Description ?? "-",
                    systemManaged);
            }

            AnsiConsole.Write(table);
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private class SystemParamList
    {
        public List<SystemParamDto> Parameters { get; set; } = [];
    }

    private record SystemParamDto(
        string Key,
        string Value,
        string DataType,
        string? Category,
        string? Description,
        bool IsSystemManaged);
}
