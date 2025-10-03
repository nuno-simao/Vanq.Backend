using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;

namespace Vanq.CLI.Commands.FeatureFlag;

/// <summary>
/// Enables or disables a feature flag.
/// </summary>
public class FeatureFlagSetCommand : BaseCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("set", "Enable or disable a feature flag");

        var keyArgument = new Argument<string>(
            "key",
            "Feature flag key");

        var enabledArgument = new Argument<bool>(
            "enabled",
            "Enable (true) or disable (false)");

        var environmentOption = new Option<string>(
            aliases: ["--environment", "-e"],
            description: "Target environment (defaults to current profile environment)");

        var descriptionOption = new Option<string>(
            aliases: ["--description", "-d"],
            description: "Feature flag description");

        command.AddArgument(keyArgument);
        command.AddArgument(enabledArgument);
        command.AddOption(environmentOption);
        command.AddOption(descriptionOption);

        command.SetHandler(async (context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArgument);
            var enabled = context.ParseResult.GetValueForArgument(enabledArgument);
            var environment = context.ParseResult.GetValueForOption(environmentOption);
            var description = context.ParseResult.GetValueForOption(descriptionOption);

            var setCommand = new FeatureFlagSetCommand();
            var exitCode = await setCommand.ExecuteAsync(
                key,
                enabled,
                environment,
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
        string key,
        bool enabled,
        string? environment,
        string? description,
        bool verbose,
        string outputFormat,
        string? profileOverride,
        bool noColor,
        bool force)
    {
        return await ExecuteWithTrackingAsync("feature-flag.set", async () =>
        {
            await InitializeAsync(verbose, outputFormat, profileOverride, noColor, force);

            RequireAuthentication();

            // Default to Development if not specified
            var targetEnvironment = environment ?? "Development";

            LogVerbose($"Setting feature flag '{key}' to {enabled} in {targetEnvironment}...");

            try
            {
                // First, check if flag exists
                var getResponse = await ApiClient.GetAsync($"/api/admin/feature-flags");

                if (getResponse.IsSuccessStatusCode)
                {
                    var existingFlags = await getResponse.Content.ReadFromJsonAsync<List<FeatureFlagDto>>();
                    var exists = existingFlags?.Any(f =>
                        f.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                        f.Environment.Equals(targetEnvironment, StringComparison.OrdinalIgnoreCase)) ?? false;

                    HttpResponseMessage response;

                    if (exists)
                    {
                        // Update existing flag
                        LogVerbose("Flag exists, updating...");
                        response = await ApiClient.PatchAsync($"/api/admin/feature-flags/{key}", new
                        {
                            environment = targetEnvironment,
                            isEnabled = enabled,
                            description
                        });
                    }
                    else
                    {
                        // Create new flag
                        LogVerbose("Flag does not exist, creating...");
                        response = await ApiClient.PostAsync("/api/admin/feature-flags", new
                        {
                            key,
                            environment = targetEnvironment,
                            isEnabled = enabled,
                            description
                        });
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return await HandleHttpErrorAsync(response);
                    }

                    var statusText = enabled ? "enabled" : "disabled";
                    LogSuccess($"Feature flag '{key}' {statusText} in {targetEnvironment}");

                    return 0;
                }
                else
                {
                    return await HandleHttpErrorAsync(getResponse);
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to set feature flag", ex);
                return 1;
            }
        });
    }

    private record FeatureFlagDto(
        string Key,
        string Environment,
        bool IsEnabled,
        string? Description,
        DateTime? UpdatedAt);
}
