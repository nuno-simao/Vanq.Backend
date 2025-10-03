using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;

namespace Vanq.CLI.Commands.SystemParam;

/// <summary>
/// Gets a specific system parameter value.
/// </summary>
public class SystemParamGetCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("get", "Get a system parameter value");

        var keyArgument = new Argument<string>(
            "key",
            "Parameter key");

        command.AddArgument(keyArgument);

        command.SetHandler(async (context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArgument);

            var getCommand = new SystemParamGetCommand();
            var exitCode = await getCommand.ExecuteAsync(
                key,
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
        string key,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("system-param.get", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            LogVerbose($"Fetching system parameter '{key}'...");

            try
            {
                var response = await ApiClient.GetAsync($"/api/admin/system-params/{Uri.EscapeDataString(key)}");

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var parameter = await response.Content.ReadFromJsonAsync<SystemParamDto>();

                if (parameter == null)
                {
                    LogError("Invalid response from server");
                    return 1;
                }

                DisplayOutput(parameter);

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Failed to fetch system parameter", ex);
                return 1;
            }
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is SystemParamDto param)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("Key", param.Key);
            table.AddRow("Value", param.Value);
            table.AddRow("Data Type", param.DataType);
            table.AddRow("Category", param.Category ?? "-");
            table.AddRow("Description", param.Description ?? "-");
            table.AddRow("System Managed", param.IsSystemManaged ? "[yellow]Yes[/]" : "No");

            if (param.ValidationRules != null && param.ValidationRules.Count > 0)
            {
                table.AddRow("Validation Rules", string.Join(", ", param.ValidationRules));
            }

            if (param.UpdatedAt.HasValue)
            {
                table.AddRow("Updated", param.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
            }

            AnsiConsole.Write(table);
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private record SystemParamDto(
        string Key,
        string Value,
        string DataType,
        string? Category,
        string? Description,
        bool IsSystemManaged,
        List<string>? ValidationRules,
        DateTime? UpdatedAt);
}
