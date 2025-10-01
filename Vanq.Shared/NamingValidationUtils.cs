using System.Text.RegularExpressions;

namespace Vanq.Shared;

/// <summary>
/// Provides utility methods for validating naming conventions.
/// </summary>
public static class NamingValidationUtils
{
    private static readonly Regex RoleNameRegex = new("^[a-z][a-z0-9-_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PermissionNameRegex = new("^[a-z][a-z0-9-]+:[a-z][a-z0-9-]+:[a-z][a-z0-9-]+(?::[a-z][a-z0-9-]+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates that a role name matches the expected pattern: ^[a-z][a-z0-9-_]+$
    /// </summary>
    /// <param name="name">The role name to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the name doesn't match the pattern.</exception>
    public static void ValidateRoleName(string name)
    {
        if (!RoleNameRegex.IsMatch(name))
        {
            throw new ArgumentException("Role name must match pattern ^[a-z][a-z0-9-_]+$", nameof(name));
        }
    }

    /// <summary>
    /// Validates that a permission name matches the expected pattern: dominio:recurso:acao[:contexto]
    /// </summary>
    /// <param name="name">The permission name to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the name doesn't match the pattern.</exception>
    public static void ValidatePermissionName(string name)
    {
        if (!PermissionNameRegex.IsMatch(name))
        {
            throw new ArgumentException("Permission name must match dominio:recurso:acao pattern", nameof(name));
        }
    }

    /// <summary>
    /// Checks if a role name is valid.
    /// </summary>
    /// <param name="name">The role name to check.</param>
    /// <returns>True if the name is valid; otherwise, false.</returns>
    public static bool IsValidRoleName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && RoleNameRegex.IsMatch(name);
    }

    /// <summary>
    /// Checks if a permission name is valid.
    /// </summary>
    /// <param name="name">The permission name to check.</param>
    /// <returns>True if the name is valid; otherwise, false.</returns>
    public static bool IsValidPermissionName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && PermissionNameRegex.IsMatch(name);
    }
}
