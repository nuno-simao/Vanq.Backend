using System;

namespace Vanq.Application.Contracts.Rbac;

public sealed record CreatePermissionRequest(
    string Name,
    string DisplayName,
    string? Description);
