using System.Collections.Generic;

namespace Vanq.Application.Contracts.Rbac;

public sealed record UpdateRoleRequest(
    string DisplayName,
    string? Description,
    IReadOnlyList<string> Permissions);
