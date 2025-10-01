namespace Vanq.Domain.Entities;

public class RolePermission
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid AddedBy { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    public Role Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    private RolePermission() { }

    private RolePermission(Guid roleId, Guid permissionId, Guid addedBy, DateTimeOffset addedAt)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        AddedBy = addedBy;
        AddedAt = addedAt;
    }

    internal static RolePermission Create(Guid roleId, Guid permissionId, Guid addedBy, DateTimeOffset addedAt)
        => new(roleId, permissionId, addedBy, addedAt);
}
