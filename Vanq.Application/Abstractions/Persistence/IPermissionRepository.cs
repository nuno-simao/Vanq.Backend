using System;
using System.Collections.Generic;
using Vanq.Domain.Entities;

namespace Vanq.Application.Abstractions.Persistence;

public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken);

    Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Permission?> GetByNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> IsAssignedAsync(Guid permissionId, CancellationToken cancellationToken);

    Task AddAsync(Permission permission, CancellationToken cancellationToken);

    void Update(Permission permission);

    void Remove(Permission permission);
}
