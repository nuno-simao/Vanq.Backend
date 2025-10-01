namespace Vanq.Application.Contracts.Rbac;

public sealed record UpdatePermissionRequest(
    string DisplayName,
    string? Description);
