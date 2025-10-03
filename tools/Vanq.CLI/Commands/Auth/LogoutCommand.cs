using System.CommandLine;
using System.CommandLine.Invocation;
using Vanq.CLI.Configuration;

namespace Vanq.CLI.Commands.Auth;

/// <summary>
/// Revokes the refresh token and deletes local credentials.
/// </summary>
public class LogoutCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("logout", "Revoke tokens and log out");

        command.SetHandler(async (context) =>
        {
            var logoutCommand = new LogoutCommand();
            var exitCode = await logoutCommand.ExecuteAsync(
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
        return await ExecuteWithTrackingAsync("auth.logout", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            if (!ApiClient.IsAuthenticated)
            {
                LogWarning("Not currently authenticated");
                return 0;
            }

            LogVerbose("Revoking refresh token...");

            try
            {
                // Try to revoke token on server
                var response = await ApiClient.PostAsync("/auth/logout", null);

                if (!response.IsSuccessStatusCode)
                {
                    LogWarning("Failed to revoke token on server, clearing local credentials anyway");
                }
                else
                {
                    LogVerbose("Token revoked on server");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to contact server: {ex.Message}");
                LogVerbose("Clearing local credentials anyway");
            }

            // Always delete local credentials
            await CredentialsManager.DeleteCredentialsAsync(CurrentProfile.Name);

            LogSuccess("Logged out successfully");

            return 0;
        });
    }
}
