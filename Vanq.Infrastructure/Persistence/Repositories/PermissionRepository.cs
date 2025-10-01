using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Repositories;

internal sealed class PermissionRepository : IPermissionRepository
{
    private readonly AppDbContext _dbContext;

    public PermissionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(permission => permission.Id == id, cancellationToken);
    }

    public Task<Permission?> GetByNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _dbContext.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(permission => permission.Name == normalizedName, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _dbContext.Permissions
            .AnyAsync(permission => permission.Name == normalizedName, cancellationToken);
    }

    public Task<bool> IsAssignedAsync(Guid permissionId, CancellationToken cancellationToken)
    {
        return _dbContext.RolePermissions
            .AnyAsync(rolePermission => rolePermission.PermissionId == permissionId, cancellationToken);
    }

    public Task AddAsync(Permission permission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return _dbContext.Permissions.AddAsync(permission, cancellationToken).AsTask();
    }

    public void Update(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _dbContext.Permissions.Update(permission);
    }

    public void Remove(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _dbContext.Permissions.Remove(permission);
    }
}
