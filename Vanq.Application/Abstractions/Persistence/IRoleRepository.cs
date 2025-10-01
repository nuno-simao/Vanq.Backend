using System;
using System.Collections.Generic;
using Vanq.Domain.Entities;

namespace Vanq.Application.Abstractions.Persistence;

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetAllWithPermissionsAsync(CancellationToken cancellationToken);

    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Role?> GetByNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken);

    Task<Role?> GetByNameWithPermissionsAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> HasActiveAssignmentsAsync(Guid roleId, CancellationToken cancellationToken);

    Task AddAsync(Role role, CancellationToken cancellationToken);

    void Update(Role role);

    void Remove(Role role);
}
