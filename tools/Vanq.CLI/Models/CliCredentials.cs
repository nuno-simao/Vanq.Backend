namespace Vanq.CLI.Models;

/// <summary>
/// Encrypted credentials stored in ~/.vanq/credentials.bin (per profile).
/// </summary>
public class CliCredentials
{
    public string Profile { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = null!;

    public CliCredentials() { }

    public CliCredentials(string profile, string accessToken, string refreshToken, DateTime expiresAt, string email)
    {
        Profile = profile;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAt = expiresAt;
        Email = email;
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
    public bool IsExpiringSoon() => DateTime.UtcNow.AddMinutes(2) >= ExpiresAt;
}
