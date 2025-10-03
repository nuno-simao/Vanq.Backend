using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;

namespace Vanq.CLI.Commands.Auth;

/// <summary>
/// Displays current user information including roles and permissions.
/// </summary>
public class WhoamiCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("whoami", "Display current user information");

        command.SetHandler(async (context) =>
        {
            var whoamiCommand = new WhoamiCommand();
            var exitCode = await whoamiCommand.ExecuteAsync(
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
        return await ExecuteWithTrackingAsync("auth.whoami", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            LogVerbose("Fetching user information...");

            try
            {
                var response = await ApiClient.GetAsync("/auth/me");

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var user = await response.Content.ReadFromJsonAsync<UserInfo>();

                if (user == null)
                {
                    LogError("Invalid response from server");
                    return 1;
                }

                DisplayOutput(user);

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Failed to fetch user information", ex);
                return 1;
            }
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is UserInfo user)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("User ID", user.Id);
            table.AddRow("Email", user.Email);
            table.AddRow("Active", user.IsActive ? "[green]Yes[/]" : "[red]No[/]");
            table.AddRow("Created", user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));

            if (user.Roles.Count > 0)
            {
                table.AddRow("Roles", string.Join(", ", user.Roles));
            }
            else
            {
                table.AddRow("Roles", "[dim]None[/]");
            }

            if (user.Permissions.Count > 0)
            {
                table.AddRow("Permissions", string.Join("\n", user.Permissions));
            }
            else
            {
                table.AddRow("Permissions", "[dim]None[/]");
            }

            AnsiConsole.Write(table);
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private record UserInfo(
        string Id,
        string Email,
        bool IsActive,
        DateTime CreatedAt,
        List<string> Roles,
        List<string> Permissions);
}
