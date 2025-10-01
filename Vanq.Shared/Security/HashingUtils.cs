using System.Security.Cryptography;
using System.Text;

namespace Vanq.Shared.Security;

/// <summary>
/// Provides utility methods for hashing operations.
/// </summary>
public static class HashingUtils
{
    /// <summary>
    /// Computes the SHA-256 hash of the input string and returns it as a hexadecimal string.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>The SHA-256 hash as an uppercase hexadecimal string.</returns>
    /// <exception cref="ArgumentException">Thrown when input is null or whitespace.</exception>
    public static string ComputeSha256Hash(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
