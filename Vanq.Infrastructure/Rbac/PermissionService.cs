using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Contracts.Rbac;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Rbac;

internal sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IPermissionRepository permissionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IFeatureFlagService featureFlagService,
        ILogger<PermissionService> logger)
    {
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAsync(CancellationToken cancellationToken)
    {
        if (!await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            throw new RbacFeatureDisabledException();
        }

        var permissions = await _permissionRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return permissions
            .OrderBy(permission => permission.Name)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<PermissionDto> CreateAsync(CreatePermissionRequest request, Guid executorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            throw new RbacFeatureDisabledException();
        }
        EnsureExecutor(executorId);

        var normalizedName = NormalizeName(request.Name);
        var exists = await _permissionRepository.ExistsByNameAsync(normalizedName, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"Permission '{normalizedName}' already exists.");
        }

        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));
        var permission = Permission.Create(normalizedName, request.DisplayName, request.Description, timestamp);

        await _permissionRepository.AddAsync(permission, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Permission {Permission} created by {Executor}", permission.Name, executorId);

        return MapToDto(permission);
    }

    public async Task<PermissionDto> UpdateAsync(Guid permissionId, UpdatePermissionRequest request, Guid executorId, CancellationToken cancellationToken)
    {
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission identifier is required.", nameof(permissionId));
        }

        ArgumentNullException.ThrowIfNull(request);
        if (!await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            throw new RbacFeatureDisabledException();
        }
        EnsureExecutor(executorId);

        var permission = await _permissionRepository.GetByIdAsync(permissionId, cancellationToken).ConfigureAwait(false);
        if (permission is null)
        {
            throw new KeyNotFoundException("Permission not found.");
        }

        permission.UpdateDetails(request.DisplayName, request.Description);
        _permissionRepository.Update(permission);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Permission {Permission} updated by {Executor}", permission.Name, executorId);
        return MapToDto(permission);
    }

    public async Task DeleteAsync(Guid permissionId, Guid executorId, CancellationToken cancellationToken)
    {
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission identifier is required.", nameof(permissionId));
        }

        if (!await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            throw new RbacFeatureDisabledException();
        }
        EnsureExecutor(executorId);

        var permission = await _permissionRepository.GetByIdAsync(permissionId, cancellationToken).ConfigureAwait(false);
        if (permission is null)
        {
            return;
        }

        var isAssigned = await _permissionRepository.IsAssignedAsync(permissionId, cancellationToken).ConfigureAwait(false);
        if (isAssigned)
        {
            throw new InvalidOperationException("Permission is assigned to a role and cannot be deleted.");
        }

        _permissionRepository.Remove(permission);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Permission {Permission} deleted by {Executor}", permission.Name, executorId);
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static void EnsureExecutor(Guid executorId)
    {
        if (executorId == Guid.Empty)
        {
            throw new ArgumentException("Executor identifier is required.", nameof(executorId));
        }
    }

    private static PermissionDto MapToDto(Permission permission)
    {
        return new PermissionDto(
            permission.Id,
            permission.Name,
            permission.DisplayName,
            permission.Description,
            permission.CreatedAt);
    }
}
