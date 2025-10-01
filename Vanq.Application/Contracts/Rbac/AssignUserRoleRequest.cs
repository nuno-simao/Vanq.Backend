using System;

namespace Vanq.Application.Contracts.Rbac;

public sealed record AssignUserRoleRequest(Guid RoleId);
