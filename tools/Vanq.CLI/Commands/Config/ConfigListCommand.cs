using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;
using Vanq.CLI.Configuration;

namespace Vanq.CLI.Commands.Config;

/// <summary>
/// Lists all configured profiles.
/// </summary>
public class ConfigListCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("list", "List all configured profiles");

        command.SetHandler(async (context) =>
        {
            var listCommand = new ConfigListCommand();
            var exitCode = await listCommand.ExecuteAsync(
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
        return await ExecuteWithTrackingAsync("config.list", async () =>
        {
            // Don't use InitializeAsync as it requires a valid profile
            Verbose = verbose;
            OutputFormat = outputFormat;
            NoColor = noColor;

            if (NoColor)
            {
                AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
            }

            var config = await ConfigManager.LoadConfigAsync();

            if (config.Profiles.Count == 0)
            {
                LogWarning("No profiles configured");
                LogInfo("Use 'vanq config add-profile' to create a profile");
                return 0;
            }

            DisplayOutput(new ProfileList
            {
                CurrentProfile = config.CurrentProfile,
                Profiles = config.Profiles.Select(p => new ProfileInfo
                {
                    Name = p.Name,
                    ApiEndpoint = p.ApiEndpoint,
                    OutputFormat = p.OutputFormat ?? "table",
                    IsCurrent = p.Name == config.CurrentProfile
                }).ToList()
            });

            return 0;
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is ProfileList profileList)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Profile");
            table.AddColumn("API Endpoint");
            table.AddColumn("Output Format");
            table.AddColumn("Current");

            foreach (var profile in profileList.Profiles)
            {
                var currentMarker = profile.IsCurrent ? "[green]✓[/]" : "";
                var profileName = profile.IsCurrent ? $"[green bold]{profile.Name}[/]" : profile.Name;

                table.AddRow(
                    profileName,
                    profile.ApiEndpoint,
                    profile.OutputFormat,
                    currentMarker);
            }

            AnsiConsole.Write(table);
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private class ProfileList
    {
        public string CurrentProfile { get; set; } = string.Empty;
        public List<ProfileInfo> Profiles { get; set; } = [];
    }

    private class ProfileInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }
}
