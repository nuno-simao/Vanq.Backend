using System.CommandLine;
using System.CommandLine.Invocation;
using Spectre.Console;
using Vanq.CLI.Configuration;
using Vanq.CLI.Models;

namespace Vanq.CLI.Commands.Config;

/// <summary>
/// Adds a new profile.
/// </summary>
public class AddProfileCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("add-profile", "Add a new profile");

        var nameArgument = new Argument<string>(
            "name",
            "Profile name");

        var apiEndpointOption = new Option<string>(
            aliases: ["--api-endpoint", "-a"],
            description: "API endpoint URL")
        {
            IsRequired = true
        };

        var outputFormatOption = new Option<string>(
            aliases: ["--output-format", "-o"],
            getDefaultValue: () => "table",
            description: "Default output format (json, table, csv)");
        outputFormatOption.FromAmong("json", "table", "csv");

        var setActiveOption = new Option<bool>(
            "--set-active",
            getDefaultValue: () => true,
            description: "Set as active profile");

        command.AddArgument(nameArgument);
        command.AddOption(apiEndpointOption);
        command.AddOption(outputFormatOption);
        command.AddOption(setActiveOption);

        command.SetHandler(async (context) =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArgument);
            var apiEndpoint = context.ParseResult.GetValueForOption(apiEndpointOption)!;
            var outputFormatOpt = context.ParseResult.GetValueForOption(outputFormatOption)!;
            var setActive = context.ParseResult.GetValueForOption(setActiveOption);

            var addProfileCommand = new AddProfileCommand();
            var exitCode = await addProfileCommand.ExecuteAsync(
                name,
                apiEndpoint,
                outputFormatOpt,
                setActive,
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
        string apiEndpoint,
        string outputFormatOpt,
        bool setActive,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("config.add-profile", async () =>
        {
            Verbose = verbose;
            NoColor = noColor;

            var config = await ConfigManager.LoadConfigAsync();

            // Check if profile already exists
            if (config.GetProfile(name) != null)
            {
                if (!force && !AnsiConsole.Confirm($"Profile '{name}' already exists. Overwrite?", defaultValue: false))
                {
                    LogInfo("Operation cancelled");
                    return 0;
                }

                // Remove existing profile
                config.Profiles.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            // Validate API endpoint
            if (!Uri.TryCreate(apiEndpoint, UriKind.Absolute, out var uri))
            {
                LogError("Invalid API endpoint URL");
                return 1;
            }

            // Add new profile
            var newProfile = new Models.Profile
            {
                Name = name,
                ApiEndpoint = apiEndpoint.TrimEnd('/'),
                OutputFormat = outputFormatOpt
            };

            config.Profiles.Add(newProfile);

            // Set as active if requested or if it's the first profile
            if (setActive || config.Profiles.Count == 1)
            {
                config.CurrentProfile = name;
            }

            await ConfigManager.SaveConfigAsync(config);

            LogSuccess($"Profile '{name}' added successfully");

            if (config.CurrentProfile == name)
            {
                LogInfo("Profile set as active");
            }

            return 0;
        });
    }
}
