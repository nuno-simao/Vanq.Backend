using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Vanq.Shared.Security;

namespace Vanq.Infrastructure.Auth.Tokens;

internal static class RefreshTokenFactory
{
    internal static (string PlainToken, string Hash, DateTime ExpiresAtUtc) Create(int days)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Base64UrlEncoder.Encode(bytes);
        var hash = HashingUtils.ComputeSha256Hash(plain);
        return (plain, hash, DateTime.UtcNow.AddDays(days));
    }

    internal static string ComputeHash(string token)
    {
        return HashingUtils.ComputeSha256Hash(token);
    }
}
