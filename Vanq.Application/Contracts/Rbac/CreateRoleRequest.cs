using System;
using System.Collections.Generic;

namespace Vanq.Application.Contracts.Rbac;

public sealed record CreateRoleRequest(
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<string> Permissions);
