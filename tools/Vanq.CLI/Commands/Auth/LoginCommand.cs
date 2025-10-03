using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;
using Vanq.CLI.Configuration;
using Vanq.CLI.Models;

namespace Vanq.CLI.Commands.Auth;

/// <summary>
/// Authenticates with the Vanq API using email and password.
/// </summary>
public class LoginCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("login", "Authenticate with email and password");

        var emailOption = new Option<string>(
            aliases: ["--email", "-e"],
            description: "User email address")
        {
            IsRequired = true
        };

        var passwordOption = new Option<string>(
            aliases: ["--password", "-p"],
            description: "User password (will prompt if not provided)");

        command.AddOption(emailOption);
        command.AddOption(passwordOption);

        command.SetHandler(async (context) =>
        {
            var email = context.ParseResult.GetValueForOption(emailOption)!;
            var password = context.ParseResult.GetValueForOption(passwordOption);

            var loginCommand = new LoginCommand();
            var exitCode = await loginCommand.ExecuteAsync(
                email,
                password,
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
        string email,
        string? password,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("auth.login", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            // Prompt for password if not provided
            if (string.IsNullOrEmpty(password))
            {
                password = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter password:")
                        .PromptStyle("yellow")
                        .Secret());
            }

            LogVerbose($"Authenticating as {email}...");

            try
            {
                var response = await ApiClient.PostAsync("/auth/login", new
                {
                    email,
                    password
                });

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (result == null)
                {
                    LogError("Invalid response from server");
                    return 1;
                }

                // Save credentials
                var credentials = new CliCredentials(
                    CurrentProfile.Name,
                    result.AccessToken,
                    result.RefreshToken,
                    DateTime.UtcNow.AddMinutes(result.ExpiresInMinutes),
                    email
                );

                await CredentialsManager.SaveCredentialsAsync(credentials);

                LogSuccess($"Authenticated as {email}");
                LogInfo($"Token expires in {result.ExpiresInMinutes} minutes");

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Authentication failed", ex);
                return 1;
            }
        });
    }

    private record LoginResponse(
        string AccessToken,
        string RefreshToken,
        int ExpiresInMinutes);
}
