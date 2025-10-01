using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vanq.Application.Contracts.Rbac;

namespace Vanq.Application.Abstractions.Rbac;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAsync(CancellationToken cancellationToken);

    Task<RoleDto?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken);

    Task<RoleDto> CreateAsync(CreateRoleRequest request, Guid executorId, CancellationToken cancellationToken);

    Task<RoleDto> UpdateAsync(Guid roleId, UpdateRoleRequest request, Guid executorId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid roleId, Guid executorId, CancellationToken cancellationToken);
}
