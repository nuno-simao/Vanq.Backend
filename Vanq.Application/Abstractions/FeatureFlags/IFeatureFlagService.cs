using Vanq.Application.Contracts.FeatureFlags;

namespace Vanq.Application.Abstractions.FeatureFlags;

public interface IFeatureFlagService
{
    /// <summary>
    /// Checks if a feature flag is enabled for the current environment.
    /// Uses cache for performance.
    /// </summary>
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feature flag value with a default fallback if not found.
    /// </summary>
    Task<bool> GetFlagOrDefaultAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feature flag by key for the current environment.
    /// </summary>
    Task<FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feature flags.
    /// </summary>
    Task<List<FeatureFlagDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all feature flags for the current environment.
    /// </summary>
    Task<List<FeatureFlagDto>> GetByEnvironmentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new feature flag.
    /// </summary>
    Task<FeatureFlagDto> CreateAsync(CreateFeatureFlagDto request, string? updatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing feature flag and invalidates cache.
    /// </summary>
    Task<FeatureFlagDto?> UpdateAsync(string key, UpdateFeatureFlagDto request, string? updatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a feature flag on/off and invalidates cache.
    /// </summary>
    Task<FeatureFlagDto?> ToggleAsync(string key, string? updatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a feature flag.
    /// </summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
