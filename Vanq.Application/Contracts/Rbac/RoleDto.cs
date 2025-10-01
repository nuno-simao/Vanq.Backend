using System;
using System.Collections.Generic;

namespace Vanq.Application.Contracts.Rbac;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystemRole,
    string SecurityStamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PermissionDto> Permissions);
