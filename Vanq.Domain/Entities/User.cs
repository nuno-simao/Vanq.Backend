using System.Collections.Generic;
using System.Linq;

namespace Vanq.Domain.Entities;

public class User
{
    private readonly List<UserRole> _roles = new();

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public string SecurityStamp { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();
    public IEnumerable<UserRole> ActiveRoles => _roles.Where(role => role.IsActive);

    private User() { }

    private User(Guid id, string email, string passwordHash, string securityStamp, DateTime createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string passwordHash, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User(Guid.NewGuid(), normalizedEmail, passwordHash, Guid.NewGuid().ToString("N"), nowUtc);
    }

    public void SetPasswordHash(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);

        PasswordHash = newHash;
        RotateSecurityStamp();
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        RotateSecurityStamp();
    }

    public UserRole AssignRole(Guid roleId, Guid assignedBy, DateTimeOffset assignedAt)
    {
        if (_roles.Any(role => role.RoleId == roleId && role.IsActive))
        {
            throw new InvalidOperationException("Role already assigned to user.");
        }

        var userRole = UserRole.Create(Id, roleId, assignedBy, assignedAt);
        _roles.Add(userRole);
        RotateSecurityStamp();

        return userRole;
    }

    public void RevokeRole(Guid roleId, DateTimeOffset revokedAt)
    {
        var activeRole = _roles.FirstOrDefault(role => role.RoleId == roleId && role.IsActive);
        if (activeRole is null)
        {
            return;
        }

        activeRole.Revoke(revokedAt);
        RotateSecurityStamp();
    }

    public bool HasActiveRole(Guid roleId) => _roles.Any(role => role.RoleId == roleId && role.IsActive);

    public bool HasAnyActiveRole() => _roles.Any(role => role.IsActive);

    private void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid().ToString("N");
    }
}
