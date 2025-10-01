namespace Vanq.Domain.Entities;

public class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;

    public bool IsActive => RevokedAt is null;

    private UserRole() { }

    private UserRole(Guid userId, Guid roleId, Guid assignedBy, DateTimeOffset assignedAt)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
    }

    internal static UserRole Create(Guid userId, Guid roleId, Guid assignedBy, DateTimeOffset assignedAt)
        => new(userId, roleId, assignedBy, assignedAt);

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt.HasValue)
        {
            return;
        }

        RevokedAt = revokedAt;
    }
}
