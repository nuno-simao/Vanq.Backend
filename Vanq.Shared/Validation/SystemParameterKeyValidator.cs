using System.Text.RegularExpressions;

namespace Vanq.Shared.Validation;

/// <summary>
/// Validates system parameter keys in dot.case format.
/// Keys must have 3-5 parts separated by dots, each part containing only lowercase letters, numbers, and hyphens.
/// Example: auth.password.min-length
/// </summary>
public static class SystemParameterKeyValidator
{
    // Pattern: 3-5 parts separated by dots, each part starts with letter, contains lowercase letters, numbers, hyphens
    private static readonly Regex KeyRegex = new(
        @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*){2,4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates that a system parameter key matches the dot.case pattern with 3-5 parts.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the key doesn't match the pattern.</exception>
    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

        if (key.Length > 150)
            throw new ArgumentException("Key cannot exceed 150 characters", nameof(key));

        if (!KeyRegex.IsMatch(key))
        {
            throw new ArgumentException(
                "Key must be in dot.case format with 3-5 parts (e.g., 'auth.password.min-length'). " +
                "Each part must start with a lowercase letter and contain only lowercase letters, numbers, and hyphens.",
                nameof(key));
        }

        // Additional validation: check for consecutive dots or leading/trailing dots
        if (key.Contains("..") || key.StartsWith('.') || key.EndsWith('.'))
        {
            throw new ArgumentException("Key cannot contain consecutive dots or start/end with a dot", nameof(key));
        }
    }

    /// <summary>
    /// Checks if a system parameter key is valid.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is valid; otherwise, false.</returns>
    public static bool IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 150)
            return false;

        if (key.Contains("..") || key.StartsWith('.') || key.EndsWith('.'))
            return false;

        return KeyRegex.IsMatch(key);
    }
}
