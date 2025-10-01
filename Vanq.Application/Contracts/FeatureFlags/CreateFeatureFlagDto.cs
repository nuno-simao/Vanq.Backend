namespace Vanq.Application.Contracts.FeatureFlags;

public sealed record CreateFeatureFlagDto(
    string Key,
    string Environment,
    bool IsEnabled,
    string? Description = null,
    bool IsCritical = false,
    string? Metadata = null);
