using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _dbContext;

    public RoleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetAllWithPermissionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .Include(role => role.Permissions)
                .ThenInclude(rolePermission => rolePermission.Permission)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
    }

    public Task<Role?> GetByNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Name == normalizedName, cancellationToken);
    }

    public Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Roles
            .Include(role => role.Permissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
    }

    public Task<Role?> GetByNameWithPermissionsAsync(string normalizedName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _dbContext.Roles
            .Include(role => role.Permissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(role => role.Name == normalizedName, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        return _dbContext.Roles
            .AnyAsync(role => role.Name == normalizedName, cancellationToken);
    }

    public Task<bool> HasActiveAssignmentsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        return _dbContext.UserRoles
            .AnyAsync(userRole => userRole.RoleId == roleId && userRole.RevokedAt == null, cancellationToken);
    }

    public Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        return _dbContext.Roles.AddAsync(role, cancellationToken).AsTask();
    }

    public void Update(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _dbContext.Roles.Update(role);
    }

    public void Remove(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _dbContext.Roles.Remove(role);
    }
}
