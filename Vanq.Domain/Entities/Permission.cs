using Vanq.Shared;

namespace Vanq.Domain.Entities;

public class Permission
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Permission() { }

    private Permission(Guid id, string name, string displayName, string? description, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        DisplayName = displayName;
        Description = description;
        CreatedAt = createdAt;
    }

    public static Permission Create(string name, string displayName, string? description, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedName = StringNormalizationUtils.NormalizeName(name);
        NamingValidationUtils.ValidatePermissionName(normalizedName);

        return new Permission(
            Guid.NewGuid(),
            normalizedName,
            displayName.Trim(),
            StringNormalizationUtils.NormalizeDescription(description),
            timestamp);
    }

    public void UpdateDetails(string displayName, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName.Trim();
        Description = StringNormalizationUtils.NormalizeDescription(description);
    }
}
