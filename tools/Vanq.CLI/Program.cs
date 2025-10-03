using System.CommandLine;
using System.Reflection;
using Spectre.Console;
using Vanq.CLI.Configuration;
using Vanq.CLI.Models;
using Vanq.CLI.Telemetry;

namespace Vanq.CLI;

/// <summary>
/// Main entry point for Vanq CLI tool.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Setup root command
        var rootCommand = new RootCommand("Vanq CLI - Management tool for Vanq.API backend");

        // Add global options
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Enable verbose output with detailed logging");

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            getDefaultValue: () => "table",
            description: "Output format: json, table, or csv");
        outputOption.FromAmong("json", "table", "csv");

        var profileOption = new Option<string?>(
            aliases: ["--profile", "-p"],
            description: "Override the active profile");

        var noColorOption = new Option<bool>(
            "--no-color",
            description: "Disable colored output");

        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Bypass confirmation prompts");

        // Add global options to root command
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(outputOption);
        rootCommand.AddGlobalOption(profileOption);
        rootCommand.AddGlobalOption(noColorOption);
        rootCommand.AddGlobalOption(forceOption);

        // Check telemetry consent on first run (only if not just showing version/help)
        if (!args.Contains("--version") && !args.Contains("--help") && !args.Contains("-h") && !args.Contains("-?"))
        {
            await CheckTelemetryConsentAsync();
        }

        // Register subcommands
        rootCommand.AddCommand(CreateAuthCommand());
        rootCommand.AddCommand(CreateConfigCommand());
        rootCommand.AddCommand(CreateRoleCommand());
        rootCommand.AddCommand(CreateFeatureFlagCommand());
        rootCommand.AddCommand(CreateSystemParamCommand());
        rootCommand.AddCommand(CreateHealthCommand());

        // Parse and invoke
        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (args.Contains("--verbose") || args.Contains("-v"))
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }
    }

    /// <summary>
    /// Checks if telemetry consent has been given and prompts if needed.
    /// </summary>
    private static async Task CheckTelemetryConsentAsync()
    {
        var config = await ConfigManager.LoadConfigAsync();

        // If telemetry settings don't exist, initialize with default
        if (config.Telemetry == null)
        {
            config.Telemetry = new TelemetrySettings
            {
                Enabled = true,
                ConsentGiven = null // Not asked yet
            };
        }

        // If consent not yet given, prompt user
        if (config.Telemetry.ConsentGiven == null)
        {
            AnsiConsole.MarkupLine("[yellow]Welcome to Vanq CLI![/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("To help improve Vanq CLI, we collect anonymous usage data.");
            AnsiConsole.MarkupLine("This includes:");
            AnsiConsole.MarkupLine("  • Commands executed (success/failure)");
            AnsiConsole.MarkupLine("  • Execution time");
            AnsiConsole.MarkupLine("  • CLI version and platform");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]No personal information, credentials, or API data is collected.[/]");
            AnsiConsole.MarkupLine("[dim]You can opt-out anytime using: vanq config telemetry disable[/]");
            AnsiConsole.WriteLine();

            var consent = AnsiConsole.Confirm("Allow anonymous telemetry?", defaultValue: true);

            config.Telemetry.ConsentGiven = consent;
            config.Telemetry.ConsentDate = DateTime.UtcNow;

            if (consent)
            {
                config.Telemetry.AnonymousId = Guid.NewGuid().ToString();
                AnsiConsole.MarkupLine("[green]Thank you! Telemetry enabled.[/]");
            }
            else
            {
                config.Telemetry.Enabled = false;
                AnsiConsole.MarkupLine("[yellow]Telemetry disabled.[/]");
            }

            await ConfigManager.SaveConfigAsync(config);
            AnsiConsole.WriteLine();
        }
    }

    private static Command CreateAuthCommand()
    {
        var authCommand = new Command("auth", "Authentication commands");
        authCommand.AddCommand(Commands.Auth.LoginCommand.CreateCommand());
        authCommand.AddCommand(Commands.Auth.LogoutCommand.CreateCommand());
        authCommand.AddCommand(Commands.Auth.WhoamiCommand.CreateCommand());
        return authCommand;
    }

    private static Command CreateConfigCommand()
    {
        var configCommand = new Command("config", "Configuration management");
        configCommand.AddCommand(Commands.Config.ConfigListCommand.CreateCommand());
        configCommand.AddCommand(Commands.Config.SetProfileCommand.CreateCommand());
        configCommand.AddCommand(Commands.Config.AddProfileCommand.CreateCommand());
        configCommand.AddCommand(Commands.Config.TelemetryCommand.CreateCommand());
        return configCommand;
    }

    private static Command CreateRoleCommand()
    {
        var roleCommand = new Command("role", "Role management");
        roleCommand.AddCommand(Commands.Role.RoleListCommand.CreateCommand());
        roleCommand.AddCommand(Commands.Role.RoleCreateCommand.CreateCommand());
        return roleCommand;
    }

    private static Command CreateFeatureFlagCommand()
    {
        var featureFlagCommand = new Command("feature-flag", "Feature flag management");
        featureFlagCommand.AddCommand(Commands.FeatureFlag.FeatureFlagListCommand.CreateCommand());
        featureFlagCommand.AddCommand(Commands.FeatureFlag.FeatureFlagSetCommand.CreateCommand());
        return featureFlagCommand;
    }

    private static Command CreateSystemParamCommand()
    {
        var systemParamCommand = new Command("system-param", "System parameter management");
        systemParamCommand.AddCommand(Commands.SystemParam.SystemParamListCommand.CreateCommand());
        systemParamCommand.AddCommand(Commands.SystemParam.SystemParamGetCommand.CreateCommand());
        return systemParamCommand;
    }

    private static Command CreateHealthCommand()
    {
        return Commands.Health.HealthCommand.CreateCommand();
    }
}
