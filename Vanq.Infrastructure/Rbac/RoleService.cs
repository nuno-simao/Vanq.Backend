using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Contracts.Rbac;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Rbac;

internal sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IRbacFeatureManager _featureManager;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IRbacFeatureManager featureManager,
        ILogger<RoleService> logger)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _featureManager = featureManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAsync(CancellationToken cancellationToken)
    {
        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);

        var roles = await _roleRepository.GetAllWithPermissionsAsync(cancellationToken).ConfigureAwait(false);
        return roles.Select(MapToDto).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);

        if (roleId == Guid.Empty)
        {
            return null;
        }

        var role = await _roleRepository.GetByIdWithPermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
        return role is null ? null : MapToDto(role);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, Guid executorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);
        EnsureExecutor(executorId);

        var normalizedName = NormalizeName(request.Name);
        var exists = await _roleRepository.ExistsByNameAsync(normalizedName, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Role '{normalizedName}' already exists.");
        }

        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        var role = Role.Create(normalizedName, request.DisplayName, request.Description, request.IsSystemRole, timestamp);

        var permissionIds = await ResolvePermissionsAsync(request.Permissions, cancellationToken).ConfigureAwait(false);
        foreach (var permissionId in permissionIds)
        {
            role.AddPermission(permissionId, executorId, timestamp);
        }

        await _roleRepository.AddAsync(role, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var persisted = await _roleRepository.GetByIdWithPermissionsAsync(role.Id, cancellationToken).ConfigureAwait(false) ?? role;
        _logger.LogInformation("Role {RoleName} created by {Executor}", role.Name, executorId);

        return MapToDto(persisted);
    }

    public async Task<RoleDto> UpdateAsync(Guid roleId, UpdateRoleRequest request, Guid executorId, CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        ArgumentNullException.ThrowIfNull(request);
        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);
        EnsureExecutor(executorId);

        var role = await _roleRepository.GetByIdWithPermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            throw new KeyNotFoundException("Role not found.");
        }

        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        role.UpdateDetails(request.DisplayName, request.Description, timestamp);

        var desiredPermissionIds = await ResolvePermissionsAsync(request.Permissions, cancellationToken).ConfigureAwait(false);

        var currentPermissionIds = role.Permissions
            .Select(permission => permission.PermissionId)
            .ToHashSet();

        if (role.IsSystemRole && currentPermissionIds.Except(desiredPermissionIds).Any())
        {
            throw new InvalidOperationException("System roles cannot lose permissions.");
        }

        foreach (var permissionId in desiredPermissionIds)
        {
            if (!currentPermissionIds.Contains(permissionId))
            {
                role.AddPermission(permissionId, executorId, timestamp);
            }
        }

        if (!role.IsSystemRole)
        {
            foreach (var permissionId in currentPermissionIds)
            {
                if (!desiredPermissionIds.Contains(permissionId))
                {
                    role.RemovePermission(permissionId, timestamp);
                }
            }
        }

        _roleRepository.Update(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var persisted = await _roleRepository.GetByIdWithPermissionsAsync(role.Id, cancellationToken).ConfigureAwait(false) ?? role;
        _logger.LogInformation("Role {RoleName} updated by {Executor}", role.Name, executorId);

        return MapToDto(persisted);
    }

    public async Task DeleteAsync(Guid roleId, Guid executorId, CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        await _featureManager.EnsureEnabledAsync(cancellationToken).ConfigureAwait(false);
        EnsureExecutor(executorId);

        var role = await _roleRepository.GetByIdWithPermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return;
        }

        if (role.IsSystemRole)
        {
            throw new InvalidOperationException("System roles cannot be deleted.");
        }

        var hasAssignments = await _roleRepository.HasActiveAssignmentsAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (hasAssignments)
        {
            throw new InvalidOperationException("Role has active assignments and cannot be deleted.");
        }

        _roleRepository.Remove(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Role {RoleName} deleted by {Executor}", role.Name, executorId);
    }

    private static RoleDto MapToDto(Role role)
    {
        var permissions = role.Permissions
            .Where(rp => rp.Permission is not null)
            .Select(rp => rp.Permission!)
            .OrderBy(permission => permission.Name)
            .Select(permission => new PermissionDto(
                permission.Id,
                permission.Name,
                permission.DisplayName,
                permission.Description,
                permission.CreatedAt))
            .ToList();

        return new RoleDto(
            role.Id,
            role.Name,
            role.DisplayName,
            role.Description,
            role.IsSystemRole,
            role.SecurityStamp,
            role.CreatedAt,
            role.UpdatedAt,
            permissions);
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static void EnsureExecutor(Guid executorId)
    {
        if (executorId == Guid.Empty)
        {
            throw new ArgumentException("Executor identifier is required.", nameof(executorId));
        }
    }

    private async Task<HashSet<Guid>> ResolvePermissionsAsync(IEnumerable<string> permissions, CancellationToken cancellationToken)
    {
        var desiredNames = permissions
            .Select(name => name.Trim().ToLowerInvariant())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (desiredNames.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var availablePermissions = await _permissionRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var availableByName = availablePermissions.ToDictionary(permission => permission.Name, StringComparer.OrdinalIgnoreCase);

        var resolved = new HashSet<Guid>();
        foreach (var desiredName in desiredNames)
        {
            if (!availableByName.TryGetValue(desiredName, out var permission))
            {
                throw new KeyNotFoundException($"Permission '{desiredName}' not found.");
            }

            resolved.Add(permission.Id);
        }

        return resolved;
    }
}
