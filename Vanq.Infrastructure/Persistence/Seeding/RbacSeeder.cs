using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Time;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.Persistence.Seeding;

public sealed class RbacSeeder
{
    private static readonly Guid SystemSeederUserId = Guid.Empty;

    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RbacSeeder> _logger;
    private readonly IOptions<RbacSeedOptions> _options;

    public RbacSeeder(
        AppDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<RbacSeeder> logger,
        IOptions<RbacSeedOptions> options)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _options = options;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedConfig = _options.Value;
        if (!seedConfig.Permissions.Any() && !seedConfig.Roles.Any())
        {
            _logger.LogInformation("No RBAC seed data configured. Skipping RBAC seeding.");
            return;
        }

        var now = DateTime.SpecifyKind(_dateTimeProvider.UtcNow, DateTimeKind.Utc);
        var timestamp = new DateTimeOffset(now);

        await EnsurePermissionsAsync(seedConfig.Permissions, timestamp, cancellationToken).ConfigureAwait(false);
        await EnsureRolesAsync(seedConfig.Roles, timestamp, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsurePermissionsAsync(IEnumerable<RbacSeedPermission> permissions, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        foreach (var permissionConfig in permissions)
        {
            var name = permissionConfig.Name.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var permission = await _dbContext.Permissions
                .FirstOrDefaultAsync(p => p.Name == name, cancellationToken)
                .ConfigureAwait(false);

            if (permission is null)
            {
                permission = Permission.Create(name, permissionConfig.DisplayName, permissionConfig.Description, timestamp);
                _dbContext.Permissions.Add(permission);
                _logger.LogInformation("Seeded permission {Permission}", name);
            }
            else
            {
                permission.UpdateDetails(permissionConfig.DisplayName, permissionConfig.Description);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureRolesAsync(IEnumerable<RbacSeedRole> roles, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        foreach (var roleConfig in roles)
        {
            var name = roleConfig.Name.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var role = await _dbContext.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Name == name, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                role = Role.Create(name, roleConfig.DisplayName, roleConfig.Description, roleConfig.IsSystemRole, timestamp);
                _dbContext.Roles.Add(role);
                _logger.LogInformation("Seeded role {Role}", name);
            }
            else
            {
                role.UpdateDetails(roleConfig.DisplayName, roleConfig.Description, timestamp);
                if (roleConfig.IsSystemRole && !role.IsSystemRole)
                {
                    role.MarkAsSystemRole(timestamp);
                }
            }

            var desiredPermissionNames = roleConfig.Permissions
                .Select(p => p.Trim().ToLowerInvariant())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (desiredPermissionNames.Count == 0)
            {
                continue;
            }

            var desiredPermissions = await _dbContext.Permissions
                .Where(p => desiredPermissionNames.Contains(p.Name))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var desired in desiredPermissions)
            {
                if (role.Permissions.All(rp => rp.PermissionId != desired.Id))
                {
                    role.AddPermission(desired.Id, SystemSeederUserId, timestamp);
                }
            }

            if (!roleConfig.IsSystemRole)
            {
                var permissionsToRemove = role.Permissions
                    .Where(rp => !desiredPermissions.Any(dp => dp.Id == rp.PermissionId))
                    .ToList();

                foreach (var permission in permissionsToRemove)
                {
                    role.RemovePermission(permission.PermissionId, timestamp);
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
