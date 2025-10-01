using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Vanq.Domain.Entities;

public class Role
{
    private static readonly Regex NameRegex = new("^[a-z][a-z0-9-_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<RolePermission> _permissions = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public string SecurityStamp { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }

    private Role(Guid id, string name, string displayName, string? description, bool isSystemRole, string securityStamp, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        DisplayName = displayName;
        Description = description;
        IsSystemRole = isSystemRole;
        SecurityStamp = securityStamp;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Role Create(string name, string displayName, string? description, bool isSystemRole, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedName = NormalizeName(name);
        ValidateName(normalizedName);

        return new Role(
            Guid.NewGuid(),
            normalizedName,
            displayName.Trim(),
            NormalizeDescription(description),
            isSystemRole,
            Guid.NewGuid().ToString("N"),
            timestamp,
            timestamp);
    }

    public void UpdateDetails(string displayName, string? description, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName.Trim();
        Description = NormalizeDescription(description);
        RotateSecurityStamp(timestamp);
    }

    public RolePermission AddPermission(Guid permissionId, Guid addedBy, DateTimeOffset timestamp)
    {
        if (_permissions.Any(p => p.PermissionId == permissionId))
        {
            throw new InvalidOperationException("Permission already associated with this role.");
        }

        var rolePermission = RolePermission.Create(Id, permissionId, addedBy, timestamp);
        _permissions.Add(rolePermission);
        RotateSecurityStamp(timestamp);
        return rolePermission;
    }

    public void RemovePermission(Guid permissionId, DateTimeOffset timestamp)
    {
        var existing = _permissions.FirstOrDefault(p => p.PermissionId == permissionId);
        if (existing is null)
        {
            return;
        }

        _permissions.Remove(existing);
        RotateSecurityStamp(timestamp);
    }

    public void MarkAsSystemRole(DateTimeOffset timestamp)
    {
        IsSystemRole = true;
        RotateSecurityStamp(timestamp);
    }

    public void RotateSecurityStamp(DateTimeOffset timestamp)
    {
        SecurityStamp = Guid.NewGuid().ToString("N");
        UpdatedAt = timestamp;
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
            throw new ArgumentException("Role name must match pattern ^[a-z][a-z0-9-_]+$", nameof(name));
        }
    }
}
