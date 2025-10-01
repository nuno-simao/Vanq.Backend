namespace Vanq.Shared.Security;

/// <summary>
/// Provides utility methods for generating security stamps.
/// </summary>
public static class SecurityStampUtils
{
    /// <summary>
    /// Generates a new security stamp using a GUID formatted without hyphens.
    /// </summary>
    /// <returns>A new security stamp string.</returns>
    public static string Generate()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Validates that a security stamp is not null or empty.
    /// </summary>
    /// <param name="securityStamp">The security stamp to validate.</param>
    /// <returns>True if the security stamp is valid; otherwise, false.</returns>
    public static bool IsValid(string? securityStamp)
    {
        return !string.IsNullOrWhiteSpace(securityStamp);
    }
}
