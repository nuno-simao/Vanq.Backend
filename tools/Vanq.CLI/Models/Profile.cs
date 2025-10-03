namespace Vanq.CLI.Models;

/// <summary>
/// Represents a CLI profile (environment configuration).
/// </summary>
public class Profile
{
    public string Name { get; set; } = null!;
    public string ApiEndpoint { get; set; } = null!;
    public string? OutputFormat { get; set; } = "table"; // json, table, csv

    public Profile() { }

    public Profile(string name, string apiEndpoint, string? outputFormat = "table")
    {
        Name = name;
        ApiEndpoint = apiEndpoint;
        OutputFormat = outputFormat;
    }
}
