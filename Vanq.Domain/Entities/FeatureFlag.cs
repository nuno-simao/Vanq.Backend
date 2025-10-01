using Vanq.Shared;

namespace Vanq.Domain.Entities;

public class FeatureFlag
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = null!;
    public string Environment { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public string? Description { get; private set; }
    public bool IsCritical { get; private set; }
    public string? LastUpdatedBy { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public string? Metadata { get; private set; }

    private FeatureFlag() { }

    private FeatureFlag(
        Guid id,
        string key,
        string environment,
        bool isEnabled,
        string? description,
        bool isCritical,
        string? lastUpdatedBy,
        DateTime lastUpdatedAt,
        string? metadata)
    {
        Id = id;
        Key = key;
        Environment = environment;
        IsEnabled = isEnabled;
        Description = description;
        IsCritical = isCritical;
        LastUpdatedBy = lastUpdatedBy;
        LastUpdatedAt = lastUpdatedAt;
        Metadata = metadata;
    }

    public static FeatureFlag Create(
        string key,
        string environment,
        bool isEnabled,
        string? description = null,
        bool isCritical = false,
        string? lastUpdatedBy = null,
        DateTime? lastUpdatedAt = null,
        string? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        // Validate key format (kebab-case convention)
        if (!key.IsValidKebabCase())
        {
            throw new ArgumentException("Feature flag key must be in kebab-case format (e.g., 'user-registration-enabled').", nameof(key));
        }

        // Limit key length as per spec (128 chars)
        if (key.Length > 128)
        {
            throw new ArgumentException("Feature flag key cannot exceed 128 characters.", nameof(key));
        }

        // Limit environment length (50 chars)
        if (environment.Length > 50)
        {
            throw new ArgumentException("Environment name cannot exceed 50 characters.", nameof(environment));
        }

        // Limit description length (256 chars)
        if (description?.Length > 256)
        {
            throw new ArgumentException("Description cannot exceed 256 characters.", nameof(description));
        }

        // Limit lastUpdatedBy length (64 chars)
        if (lastUpdatedBy?.Length > 64)
        {
            throw new ArgumentException("LastUpdatedBy cannot exceed 64 characters.", nameof(lastUpdatedBy));
        }

        return new FeatureFlag(
            Guid.NewGuid(),
            key.ToLowerInvariant(),
            environment,
            isEnabled,
            description,
            isCritical,
            lastUpdatedBy,
            lastUpdatedAt ?? DateTime.UtcNow,
            metadata);
    }

    public void Update(
        bool isEnabled,
        string? description = null,
        string? lastUpdatedBy = null,
        DateTime? lastUpdatedAt = null,
        string? metadata = null)
    {
        // Limit description length
        if (description?.Length > 256)
        {
            throw new ArgumentException("Description cannot exceed 256 characters.", nameof(description));
        }

        // Limit lastUpdatedBy length
        if (lastUpdatedBy?.Length > 64)
        {
            throw new ArgumentException("LastUpdatedBy cannot exceed 64 characters.", nameof(lastUpdatedBy));
        }

        IsEnabled = isEnabled;
        if (description is not null) Description = description;
        LastUpdatedBy = lastUpdatedBy;
        LastUpdatedAt = lastUpdatedAt ?? DateTime.UtcNow;
        if (metadata is not null) Metadata = metadata;
    }

    public void Toggle(string? lastUpdatedBy = null, DateTime? lastUpdatedAt = null)
    {
        IsEnabled = !IsEnabled;
        LastUpdatedBy = lastUpdatedBy;
        LastUpdatedAt = lastUpdatedAt ?? DateTime.UtcNow;
    }

    // private static bool IsValidKebabCase(string key)
    // {
    //     // kebab-case: lowercase letters, numbers, and hyphens only
    //     // Must start with a letter, cannot have consecutive hyphens
    //     if (string.IsNullOrWhiteSpace(key)) return false;
    //     if (!char.IsLetter(key[0])) return false;

    //     for (int i = 0; i < key.Length; i++)
    //     {
    //         char c = key[i];
    //         if (!char.IsLower(c) && !char.IsDigit(c) && c != '-')
    //             return false;

    //         // No consecutive hyphens
    //         if (c == '-' && i + 1 < key.Length && key[i + 1] == '-')
    //             return false;
    //     }

    //     // Cannot end with hyphen
    //     return key[^1] != '-';
    // }
}
