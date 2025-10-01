using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Configuration;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Rbac;

internal sealed class UserRoleService : IUserRoleService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IRbacFeatureManager _featureManager;
    private readonly IOptions<RbacOptions> _options;
    private readonly ILogger<UserRoleService> _logger;

    public UserRoleService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IRbacFeatureManager featureManager,
        IOptions<RbacOptions> options,
        ILogger<UserRoleService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _featureManager = featureManager;
        _options = options;
        _logger = logger;
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, Guid executorId, CancellationToken cancellationToken)
    {
        EnsureIdentifiers(userId, roleId, executorId);
        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);

        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException("User not found.");

        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException("Role not found.");

        if (user.HasActiveRole(roleId))
        {
            return;
        }

        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        user.AssignRole(roleId, executorId, timestamp);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Role {RoleName} assigned to user {UserId} by {Executor}", role.Name, userId, executorId);
    }

    public async Task RevokeRoleAsync(Guid userId, Guid roleId, Guid executorId, CancellationToken cancellationToken)
    {
        EnsureIdentifiers(userId, roleId, executorId);
        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);

        var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken).ConfigureAwait(false)
                   ?? throw new KeyNotFoundException("User not found.");

        var activeRole = user.Roles.FirstOrDefault(assignment => assignment.RoleId == roleId && assignment.IsActive);
        if (activeRole is null)
        {
            return;
        }

        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        user.RevokeRole(roleId, timestamp);

        if (!user.HasAnyActiveRole())
        {
            await AssignDefaultRoleAsync(user, executorId, timestamp, cancellationToken).ConfigureAwait(false);
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Role {RoleId} revoked from user {UserId} by {Executor}", roleId, userId, executorId);
    }

    private static void EnsureIdentifiers(Guid userId, Guid roleId, Guid executorId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        if (executorId == Guid.Empty)
        {
            throw new ArgumentException("Executor identifier is required.", nameof(executorId));
        }
    }

    private async Task AssignDefaultRoleAsync(User user, Guid executorId, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        var defaultRoleName = _options.Value.DefaultRole;
        if (string.IsNullOrWhiteSpace(defaultRoleName))
        {
            throw new InvalidOperationException("Default role is not configured.");
        }

        var normalizedName = defaultRoleName.Trim().ToLowerInvariant();
        var defaultRole = await _roleRepository.GetByNameAsync(normalizedName, cancellationToken).ConfigureAwait(false)
                          ?? throw new InvalidOperationException("Default role does not exist.");

        if (!user.HasActiveRole(defaultRole.Id))
        {
            user.AssignRole(defaultRole.Id, executorId, timestamp);
        }
    }
}
