namespace Vanq.Shared;

/// <summary>
/// Provides utility methods for building cache keys.
/// </summary>
public static class CacheKeyUtils
{
    /// <summary>
    /// Builds a cache key by joining a prefix and segments with colons.
    /// </summary>
    /// <param name="prefix">The prefix for the cache key.</param>
    /// <param name="segments">The segments to append to the prefix.</param>
    /// <returns>A formatted cache key string.</returns>
    public static string BuildKey(string prefix, params string[] segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        
        if (segments == null || segments.Length == 0)
        {
            return prefix;
        }

        return $"{prefix}:{string.Join(':', segments)}";
    }

    /// <summary>
    /// Builds a cache key specifically for feature flags.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <param name="flagKey">The feature flag key.</param>
    /// <returns>A formatted feature flag cache key.</returns>
    public static string BuildFeatureFlagKey(string environment, string flagKey)
    {
        return BuildKey("feature-flag", environment, flagKey);
    }

    /// <summary>
    /// Builds a cache key for user-related data.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="suffix">Optional suffix for the key.</param>
    /// <returns>A formatted user cache key.</returns>
    public static string BuildUserKey(Guid userId, string? suffix = null)
    {
        return suffix == null 
            ? BuildKey("user", userId.ToString("N")) 
            : BuildKey("user", userId.ToString("N"), suffix);
    }
}
