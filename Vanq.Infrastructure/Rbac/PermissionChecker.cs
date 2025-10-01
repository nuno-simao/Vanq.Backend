using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;

namespace Vanq.Infrastructure.Rbac;

internal sealed class PermissionChecker : IPermissionChecker
{
    private readonly IUserRepository _userRepository;
    private readonly IRbacFeatureManager _featureManager;
    private readonly ILogger<PermissionChecker> _logger;
    private readonly Dictionary<(Guid, string), bool> _cache = new();

    public PermissionChecker(
        IUserRepository userRepository,
        IRbacFeatureManager featureManager,
        ILogger<PermissionChecker> logger)
    {
        _userRepository = userRepository;
        _featureManager = featureManager;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (!_featureManager.IsEnabled)
        {
            return true;
        }

        var normalizedPermission = permission.Trim().ToLowerInvariant();
        if (_cache.TryGetValue((userId, normalizedPermission), out var cached))
        {
            return cached;
        }

        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            _cache[(userId, normalizedPermission)] = false;
            return false;
        }

        var hasPermission = user.Roles
            .Where(role => role.IsActive && role.Role is not null)
            .SelectMany(role => role.Role!.Permissions)
            .Any(rolePermission => rolePermission.Permission is not null && string.Equals(rolePermission.Permission.Name, normalizedPermission, StringComparison.OrdinalIgnoreCase));

        _cache[(userId, normalizedPermission)] = hasPermission;
        return hasPermission;
    }

    public async Task EnsurePermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var hasPermission = await HasPermissionAsync(userId, permission, cancellationToken).ConfigureAwait(false);
        if (!hasPermission)
        {
            var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken).ConfigureAwait(false);
            var activeRoles = user?.Roles
                .Where(role => role.IsActive)
                .Select(role => role.RoleId)
                .ToArray() ?? Array.Empty<Guid>();

            _logger.LogWarning(
                "User {UserId} lacks permission {Permission}. ActiveRoles={ActiveRoles}",
                userId,
                permission,
                activeRoles);
            throw new UnauthorizedAccessException("User does not have required permission.");
        }
    }
}
