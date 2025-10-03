using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Vanq.CLI.Commands.Role;

/// <summary>
/// Creates a new role.
/// </summary>
public class RoleCreateCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("create", "Create a new role");

        var nameArgument = new Argument<string>(
            "name",
            "Role name (lowercase, alphanumeric, hyphens, underscores)");

        var displayNameOption = new Option<string>(
            aliases: ["--display-name", "-d"],
            description: "Display name for the role")
        {
            IsRequired = true
        };

        var descriptionOption = new Option<string>(
            aliases: ["--description", "-desc"],
            description: "Role description");

        command.AddArgument(nameArgument);
        command.AddOption(displayNameOption);
        command.AddOption(descriptionOption);

        command.SetHandler(async (context) =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArgument);
            var displayName = context.ParseResult.GetValueForOption(displayNameOption)!;
            var description = context.ParseResult.GetValueForOption(descriptionOption);

            var createCommand = new RoleCreateCommand();
            var exitCode = await createCommand.ExecuteAsync(
                name,
                displayName,
                description,
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
        string displayName,
        string? description,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("role.create", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            LogVerbose($"Creating role '{name}'...");

            try
            {
                var response = await ApiClient.PostAsync("/auth/roles", new
                {
                    name,
                    displayName,
                    description
                });

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var role = await response.Content.ReadFromJsonAsync<RoleDto>();

                if (role == null)
                {
                    LogError("Invalid response from server");
                    return 1;
                }

                LogSuccess($"Role '{name}' created successfully");
                LogInfo($"Role ID: {role.Id}");

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Failed to create role", ex);
                return 1;
            }
        });
    }

    private record RoleDto(
        string Id,
        string Name,
        string? DisplayName,
        string? Description);
}
