using System.Text.Json;
using Vanq.CLI.Models;

namespace Vanq.CLI.Configuration;

/// <summary>
/// Manages CLI configuration (profiles, settings).
/// </summary>
public class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<CliConfig> LoadConfigAsync()
    {
        PathProvider.EnsureDirectoriesExist();

        if (!File.Exists(PathProvider.ConfigFilePath))
        {
            // Create default config
            var defaultConfig = new CliConfig();
            await SaveConfigAsync(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(PathProvider.ConfigFilePath);
            return JsonSerializer.Deserialize<CliConfig>(json, JsonOptions) ?? new CliConfig();
        }
        catch
        {
            // If config is corrupted, return default
            return new CliConfig();
        }
    }

    public static async Task SaveConfigAsync(CliConfig config)
    {
        PathProvider.EnsureDirectoriesExist();

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(PathProvider.ConfigFilePath, json);
    }

    public static async Task<Profile?> GetCurrentProfileAsync()
    {
        var config = await LoadConfigAsync();
        return config.GetCurrentProfile();
    }

    public static async Task SetCurrentProfileAsync(string profileName)
    {
        var config = await LoadConfigAsync();
        var profile = config.GetProfile(profileName);

        if (profile == null)
            throw new InvalidOperationException($"Profile '{profileName}' not found");

        config.CurrentProfile = profileName;
        await SaveConfigAsync(config);
    }

    public static async Task AddProfileAsync(Profile profile)
    {
        var config = await LoadConfigAsync();
        config.AddOrUpdateProfile(profile);
        await SaveConfigAsync(config);
    }

    public static async Task<bool> RemoveProfileAsync(string profileName)
    {
        var config = await LoadConfigAsync();

        if (config.CurrentProfile.Equals(profileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot remove the active profile");

        var removed = config.RemoveProfile(profileName);
        if (removed)
            await SaveConfigAsync(config);

        return removed;
    }
}
