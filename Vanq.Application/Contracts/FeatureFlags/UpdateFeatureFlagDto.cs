namespace Vanq.Application.Contracts.FeatureFlags;

public sealed record UpdateFeatureFlagDto(
    bool IsEnabled,
    string? Description = null,
    string? Metadata = null);
