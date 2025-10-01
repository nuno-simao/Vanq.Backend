namespace Vanq.Application.Contracts.FeatureFlags;

public sealed record FeatureFlagDto(
    Guid Id,
    string Key,
    string Environment,
    bool IsEnabled,
    string? Description,
    bool IsCritical,
    string? LastUpdatedBy,
    DateTime LastUpdatedAt,
    string? Metadata);
