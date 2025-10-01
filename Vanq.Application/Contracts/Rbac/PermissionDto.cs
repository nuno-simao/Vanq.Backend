using System;

namespace Vanq.Application.Contracts.Rbac;

public sealed record PermissionDto(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    DateTimeOffset CreatedAt);
