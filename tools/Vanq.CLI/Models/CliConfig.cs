namespace Vanq.CLI.Models;

/// <summary>
/// CLI configuration stored in ~/.vanq/config.json
/// </summary>
public class CliConfig
{
    public string CurrentProfile { get; set; } = "default";
    public List<Profile> Profiles { get; set; } = new();
    public TelemetrySettings? Telemetry { get; set; }

    public CliConfig()
    {
        // Initialize with default profile
        Profiles.Add(new Profile("default", "http://localhost:5000", "table"));
    }

    public Profile? GetCurrentProfile()
    {
        return Profiles.FirstOrDefault(p => p.Name == CurrentProfile);
    }

    public Profile? GetProfile(string name)
    {
        return Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void AddOrUpdateProfile(Profile profile)
    {
        var existing = GetProfile(profile.Name);
        if (existing != null)
        {
            existing.ApiEndpoint = profile.ApiEndpoint;
            existing.OutputFormat = profile.OutputFormat;
        }
        else
        {
            Profiles.Add(profile);
        }
    }

    public bool RemoveProfile(string name)
    {
        var profile = GetProfile(name);
        if (profile != null)
        {
            Profiles.Remove(profile);
            return true;
        }
        return false;
    }
}
