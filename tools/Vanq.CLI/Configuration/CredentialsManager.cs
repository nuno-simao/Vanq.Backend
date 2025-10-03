using Vanq.CLI.Models;
using Vanq.CLI.Services;

namespace Vanq.CLI.Configuration;

/// <summary>
/// Manages encrypted credentials per profile.
/// </summary>
public class CredentialsManager
{
    public static async Task<CliCredentials?> LoadCredentialsAsync(string profileName)
    {
        PathProvider.EnsureDirectoriesExist();

        if (!File.Exists(PathProvider.CredentialsFilePath))
            return null;

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(PathProvider.CredentialsFilePath);
            var allCredentials = CredentialEncryption.Decrypt<List<CliCredentials>>(encryptedData);

            return allCredentials?.FirstOrDefault(c =>
                c.Profile.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    public static async Task SaveCredentialsAsync(CliCredentials credentials)
    {
        PathProvider.EnsureDirectoriesExist();

        var allCredentials = new List<CliCredentials>();

        // Load existing credentials for other profiles
        if (File.Exists(PathProvider.CredentialsFilePath))
        {
            try
            {
                var encryptedData = await File.ReadAllBytesAsync(PathProvider.CredentialsFilePath);
                var existing = CredentialEncryption.Decrypt<List<CliCredentials>>(encryptedData);
                if (existing != null)
                {
                    // Remove old credentials for this profile
                    allCredentials = existing.Where(c =>
                        !c.Profile.Equals(credentials.Profile, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }
            }
            catch
            {
                // Ignore corrupted credentials file
            }
        }

        // Add new credentials
        allCredentials.Add(credentials);

        // Encrypt and save
        var encrypted = CredentialEncryption.Encrypt(allCredentials);
        await File.WriteAllBytesAsync(PathProvider.CredentialsFilePath, encrypted);
    }

    public static async Task DeleteCredentialsAsync(string profileName)
    {
        if (!File.Exists(PathProvider.CredentialsFilePath))
            return;

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(PathProvider.CredentialsFilePath);
            var allCredentials = CredentialEncryption.Decrypt<List<CliCredentials>>(encryptedData);

            if (allCredentials == null || allCredentials.Count == 0)
                return;

            // Remove credentials for this profile
            var remaining = allCredentials.Where(c =>
                !c.Profile.Equals(profileName, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (remaining.Count == 0)
            {
                // No more credentials, delete file
                File.Delete(PathProvider.CredentialsFilePath);
            }
            else
            {
                // Save remaining credentials
                var encrypted = CredentialEncryption.Encrypt(remaining);
                await File.WriteAllBytesAsync(PathProvider.CredentialsFilePath, encrypted);
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    public static async Task<CliCredentials?> GetCurrentCredentialsAsync()
    {
        var profile = await ConfigManager.GetCurrentProfileAsync();
        if (profile == null)
            return null;

        return await LoadCredentialsAsync(profile.Name);
    }
}
