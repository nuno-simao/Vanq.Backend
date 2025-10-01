using System.Collections.Generic;

namespace Vanq.Infrastructure.Persistence.Seeding;

public sealed class RbacSeedOptions
{
    public const string SectionName = "Rbac:Seed";

    public List<RbacSeedPermission> Permissions { get; set; } = new();
    public List<RbacSeedRole> Roles { get; set; } = new();
    public string DefaultRole { get; set; } = "viewer";
}

public sealed class RbacSeedPermission
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class RbacSeedRole
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public List<string> Permissions { get; set; } = new();
}
