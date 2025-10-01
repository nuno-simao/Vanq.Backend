namespace Vanq.Shared;

/// <summary>
/// Provides utility methods for normalizing common string values.
/// </summary>
public static class StringNormalizationUtils
{
    /// <summary>
    /// Normalizes a name by trimming whitespace and converting to lowercase invariant.
    /// </summary>
    /// <param name="name">The name to normalize.</param>
    /// <returns>The normalized name.</returns>
    public static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    /// <summary>
    /// Normalizes an email address by trimming whitespace and converting to lowercase invariant.
    /// </summary>
    /// <param name="email">The email to normalize.</param>
    /// <returns>The normalized email.</returns>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Normalizes a description by trimming whitespace. Returns null if the input is null or whitespace.
    /// </summary>
    /// <param name="description">The description to normalize.</param>
    /// <returns>The normalized description or null if input is empty.</returns>
    public static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
