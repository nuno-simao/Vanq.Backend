using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vanq.Application.Contracts.Rbac;

namespace Vanq.Application.Abstractions.Rbac;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionDto>> GetAsync(CancellationToken cancellationToken);

    Task<PermissionDto> CreateAsync(CreatePermissionRequest request, Guid executorId, CancellationToken cancellationToken);

    Task<PermissionDto> UpdateAsync(Guid permissionId, UpdatePermissionRequest request, Guid executorId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid permissionId, Guid executorId, CancellationToken cancellationToken);
}
