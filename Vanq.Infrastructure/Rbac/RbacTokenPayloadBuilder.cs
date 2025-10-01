using System;
using System.Collections.Generic;
using System.Linq;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Rbac;

public static class RbacTokenPayloadBuilder
{
    public static (IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions, string RolesSecurityStamp) Build(User user)
    {
        var activeAssignments = user.Roles
            .Where(role => role.IsActive && role.Role is not null)
            .ToList();

        var roles = activeAssignments
            .Select(role => role.Role!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();

        var permissions = activeAssignments
            .SelectMany(role => role.Role!.Permissions)
            .Where(permission => permission.Permission is not null)
            .Select(permission => permission.Permission!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();

        var rolesStamp = string.Join(
            ";",
            activeAssignments
                .Where(role => role.Role is not null)
                .Select(role => $"{role.RoleId:N}:{role.Role!.SecurityStamp}")
                .OrderBy(value => value));

        return (roles, permissions, rolesStamp);
    }
}
