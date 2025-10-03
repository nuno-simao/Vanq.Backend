using System.CommandLine;
using System.CommandLine.Invocation;
using Vanq.CLI.Configuration;

namespace Vanq.CLI.Commands.Config;

/// <summary>
/// Sets the active profile.
/// </summary>
public class SetProfileCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("set-profile", "Set the active profile");

        var nameArgument = new Argument<string>(
            "name",
            "Profile name to activate");

        command.AddArgument(nameArgument);

        command.SetHandler(async (context) =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArgument);

            var setProfileCommand = new SetProfileCommand();
            var exitCode = await setProfileCommand.ExecuteAsync(
                name,
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
        string name,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("config.set-profile", async () =>
        {
            Verbose = verbose;
            NoColor = noColor;

            var config = await ConfigManager.LoadConfigAsync();

            var profile = config.GetProfile(name);

            if (profile == null)
            {
                LogError($"Profile '{name}' not found");
                LogInfo("Use 'vanq config list' to see available profiles");
                return 1;
            }

            config.CurrentProfile = name;
            await ConfigManager.SaveConfigAsync(config);

            LogSuccess($"Active profile set to '{name}'");

            return 0;
        });
    }
}
