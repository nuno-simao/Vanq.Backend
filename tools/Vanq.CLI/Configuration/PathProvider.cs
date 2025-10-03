namespace Vanq.CLI.Configuration;

/// <summary>
/// Provides standard paths for CLI configuration and data files.
/// </summary>
public static class PathProvider
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".vanq"
    );

    public static string ConfigFilePath => Path.Combine(BaseDirectory, "config.json");
    public static string CredentialsFilePath => Path.Combine(BaseDirectory, "credentials.bin");
    public static string LogDirectory => Path.Combine(BaseDirectory, "logs");
    public static string LogFilePath => Path.Combine(LogDirectory, "vanq-cli.log");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
