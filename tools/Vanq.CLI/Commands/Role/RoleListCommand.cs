using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using Spectre.Console;

namespace Vanq.CLI.Commands.Role;

/// <summary>
/// Lists all roles in the system.
/// </summary>
public class RoleListCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("list", "List all roles");

        command.SetHandler(async (context) =>
        {
            var listCommand = new RoleListCommand();
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
        return await ExecuteWithTrackingAsync("role.list", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            LogVerbose("Fetching roles...");

            try
            {
                var response = await ApiClient.GetAsync("/auth/roles");

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync(response);
                }

                var roles = await response.Content.ReadFromJsonAsync<List<RoleDto>>();

                if (roles == null || roles.Count == 0)
                {
                    LogWarning("No roles found");
                    return 0;
                }

                DisplayOutput(new RoleList { Roles = roles });

                return 0;
            }
            catch (Exception ex)
            {
                LogError("Failed to fetch roles", ex);
                return 1;
            }
        });
    }

    protected override void DisplayTable<T>(T data)
    {
        if (data is RoleList roleList)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Display Name");
            table.AddColumn("Description");
            table.AddColumn("Permissions");
            table.AddColumn("System Role");

            foreach (var role in roleList.Roles)
            {
                var systemRoleMarker = role.IsSystemRole ? "[yellow]✓[/]" : "";
                var roleName = role.IsSystemRole ? $"[yellow]{role.Name}[/]" : role.Name;

                table.AddRow(
                    roleName,
                    role.DisplayName ?? "-",
                    role.Description ?? "-",
                    role.Permissions.Count.ToString(),
                    systemRoleMarker);
            }

            AnsiConsole.Write(table);
        }
        else
        {
            base.DisplayTable(data);
        }
    }

    private class RoleList
    {
        public List<RoleDto> Roles { get; set; } = [];
    }

    private record RoleDto(
        string Id,
        string Name,
        string? DisplayName,
        string? Description,
        bool IsSystemRole,
        List<string> Permissions);
}
