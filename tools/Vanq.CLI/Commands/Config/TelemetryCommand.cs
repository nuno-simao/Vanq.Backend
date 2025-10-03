using System.CommandLine;
using System.CommandLine.Invocation;
using Vanq.CLI.Configuration;

namespace Vanq.CLI.Commands.Config;

/// <summary>
/// Enables or disables telemetry.
/// </summary>
public class TelemetryCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("telemetry", "Enable or disable telemetry");

        var actionArgument = new Argument<string>(
            "action",
            "Action to perform (enable or disable)");
        actionArgument.FromAmong("enable", "disable");

        command.AddArgument(actionArgument);

        command.SetHandler(async (context) =>
        {
            var action = context.ParseResult.GetValueForArgument(actionArgument);

            var telemetryCommand = new TelemetryCommand();
            var exitCode = await telemetryCommand.ExecuteAsync(
                action,
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
        string action,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("config.telemetry", async () =>
        {
            Verbose = verbose;
            NoColor = noColor;

            var config = await ConfigManager.LoadConfigAsync();

            if (config.Telemetry == null)
            {
                config.Telemetry = new Models.TelemetrySettings();
            }

            var isEnabling = action.Equals("enable", StringComparison.OrdinalIgnoreCase);

            config.Telemetry.Enabled = isEnabling;
            config.Telemetry.ConsentGiven = isEnabling;
            config.Telemetry.ConsentDate = DateTime.UtcNow;

            // Generate anonymous ID if enabling and not present
            if (isEnabling && string.IsNullOrEmpty(config.Telemetry.AnonymousId))
            {
                config.Telemetry.AnonymousId = Guid.NewGuid().ToString();
            }

            await ConfigManager.SaveConfigAsync(config);

            if (isEnabling)
            {
                LogSuccess("Telemetry enabled");
                LogInfo("Thank you for helping improve Vanq CLI!");
            }
            else
            {
                LogSuccess("Telemetry disabled");
            }

            return 0;
        });
    }
}
