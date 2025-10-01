using System.Text.RegularExpressions;

namespace Vanq.Domain.Entities;

public class Permission
{
    private static readonly Regex NameRegex = new("^[a-z][a-z0-9-]+:[a-z][a-z0-9-]+:[a-z][a-z0-9-]+(?::[a-z][a-z0-9-]+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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

        var normalizedName = NormalizeName(name);
        ValidateName(normalizedName);

        return new Permission(
            Guid.NewGuid(),
            normalizedName,
            displayName.Trim(),
            NormalizeDescription(description),
            timestamp);
    }

    public void UpdateDetails(string displayName, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName.Trim();
        Description = NormalizeDescription(description);
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static void ValidateName(string name)
    {
        if (!NameRegex.IsMatch(name))
        {
            throw new ArgumentException("Permission name must match dominio:recurso:acao pattern", nameof(name));
        }
    }
}
