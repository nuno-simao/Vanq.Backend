using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Vanq.Infrastructure.Auth.Tokens;

internal static class RefreshTokenFactory
{
    internal static (string PlainToken, string Hash, DateTime ExpiresAtUtc) Create(int days)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Base64UrlEncoder.Encode(bytes);
        var hash = ComputeHash(plain);
        return (plain, hash, DateTime.UtcNow.AddDays(days));
    }

    internal static string ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
