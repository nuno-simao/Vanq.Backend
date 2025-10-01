using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Contracts.FeatureFlags;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Logging.Extensions;
using Vanq.Shared;

namespace Vanq.Infrastructure.FeatureFlags;

internal sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly IHostEnvironment _environment;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<FeatureFlagService> _logger;

    private const int CacheDurationSeconds = 60;

    public FeatureFlagService(
        IFeatureFlagRepository repository,
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        IHostEnvironment environment,
        IDateTimeProvider clock,
        ILogger<FeatureFlagService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _environment = environment;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = key.ToLowerInvariant();
        var cacheKey = CacheKeyUtils.BuildFeatureFlagKey(_environment.EnvironmentName, normalizedKey);

        // Try cache first
        if (_cache.TryGetValue<bool>(cacheKey, out var cachedValue))
        {
            _logger.LogFeatureFlagEvent(normalizedKey, cachedValue, _environment.EnvironmentName, "cache hit");
            return cachedValue;
        }

        // Cache miss - query database
        try
        {
            var flag = await _repository.GetByKeyAndEnvironmentAsync(
                normalizedKey,
                _environment.EnvironmentName,
                cancellationToken);

            var isEnabled = flag?.IsEnabled ?? false;

            // Cache the result
            _cache.Set(cacheKey, isEnabled, TimeSpan.FromSeconds(CacheDurationSeconds));

            _logger.LogFeatureFlagEvent(normalizedKey, isEnabled, _environment.EnvironmentName, "database load");

            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading feature flag '{Key}' from database. Returning false as fallback.",
                normalizedKey);
            return false;
        }
    }

    public async Task<bool> GetFlagOrDefaultAsync(
        string key,
        bool defaultValue = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = key.ToLowerInvariant();
        var cacheKey = CacheKeyUtils.BuildFeatureFlagKey(_environment.EnvironmentName, normalizedKey);

        // Try cache first
        if (_cache.TryGetValue<bool>(cacheKey, out var cachedValue))
        {
            _logger.LogFeatureFlagEvent(normalizedKey, cachedValue, _environment.EnvironmentName, "cache hit");
            return cachedValue;
        }

        // Cache miss - query database
        try
        {
            var flag = await _repository.GetByKeyAndEnvironmentAsync(
                normalizedKey,
                _environment.EnvironmentName,
                cancellationToken);

            var isEnabled = flag?.IsEnabled ?? defaultValue;

            // Cache the result
            _cache.Set(cacheKey, isEnabled, TimeSpan.FromSeconds(CacheDurationSeconds));

            _logger.LogFeatureFlagEvent(normalizedKey, isEnabled, _environment.EnvironmentName,
                flag is null ? $"flag not found, using default: {defaultValue}" : "database load");

            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error loading feature flag '{Key}'. Using default value: {DefaultValue}",
                normalizedKey,
                defaultValue);
            return defaultValue;
        }
    }

    public async Task<FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var flag = await _repository.GetByKeyAndEnvironmentAsync(
            key.ToLowerInvariant(),
            _environment.EnvironmentName,
            cancellationToken);

        return flag is null ? null : MapToDto(flag);
    }

    public async Task<List<FeatureFlagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var flags = await _repository.GetAllAsync(cancellationToken);
        return flags.Select(MapToDto).ToList();
    }

    public async Task<List<FeatureFlagDto>> GetByEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        var flags = await _repository.GetByEnvironmentAsync(
            _environment.EnvironmentName,
            cancellationToken);
        return flags.Select(MapToDto).ToList();
    }

    public async Task<FeatureFlagDto> CreateAsync(
        CreateFeatureFlagDto request,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check if already exists
        var exists = await _repository.ExistsByKeyAndEnvironmentAsync(
            request.Key,
            request.Environment,
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Feature flag with key '{request.Key}' already exists in environment '{request.Environment}'.");
        }

        var flag = FeatureFlag.Create(
            key: request.Key,
            environment: request.Environment,
            isEnabled: request.IsEnabled,
            description: request.Description,
            isCritical: request.IsCritical,
            lastUpdatedBy: updatedBy,
            lastUpdatedAt: _clock.UtcNow,
            metadata: request.Metadata);

        await _repository.AddAsync(flag, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache for this flag
        InvalidateCache(request.Key);

        _logger.LogInformation(
            "Feature flag created: Key={Key}, Environment={Environment}, IsEnabled={IsEnabled}, UpdatedBy={UpdatedBy}",
            request.Key,
            request.Environment,
            request.IsEnabled,
            updatedBy ?? "unknown");

        return MapToDto(flag);
    }

    public async Task<FeatureFlagDto?> UpdateAsync(
        string key,
        UpdateFeatureFlagDto request,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);

        var flag = await _repository.GetByKeyAndEnvironmentAsync(
            key.ToLowerInvariant(),
            _environment.EnvironmentName,
            cancellationToken,
            track: true);

        if (flag is null)
        {
            _logger.LogWarning(
                "Feature flag not found for update: Key={Key}, Environment={Environment}",
                key,
                _environment.EnvironmentName);
            return null;
        }

        var oldValue = flag.IsEnabled;

        flag.Update(
            isEnabled: request.IsEnabled,
            description: request.Description,
            lastUpdatedBy: updatedBy,
            lastUpdatedAt: _clock.UtcNow,
            metadata: request.Metadata);

        _repository.Update(flag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        InvalidateCache(key);

        _logger.LogInformation(
            "Feature flag updated: Key={Key}, Environment={Environment}, OldValue={OldValue}, NewValue={NewValue}, UpdatedBy={UpdatedBy}",
            key,
            _environment.EnvironmentName,
            oldValue,
            request.IsEnabled,
            updatedBy ?? "unknown");

        return MapToDto(flag);
    }

    public async Task<FeatureFlagDto?> ToggleAsync(
        string key,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var flag = await _repository.GetByKeyAndEnvironmentAsync(
            key.ToLowerInvariant(),
            _environment.EnvironmentName,
            cancellationToken,
            track: true);

        if (flag is null)
        {
            _logger.LogWarning(
                "Feature flag not found for toggle: Key={Key}, Environment={Environment}",
                key,
                _environment.EnvironmentName);
            return null;
        }

        var oldValue = flag.IsEnabled;

        flag.Toggle(
            lastUpdatedBy: updatedBy,
            lastUpdatedAt: _clock.UtcNow);

        _repository.Update(flag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        InvalidateCache(key);

        _logger.LogInformation(
            "Feature flag toggled: Key={Key}, Environment={Environment}, OldValue={OldValue}, NewValue={NewValue}, UpdatedBy={UpdatedBy}",
            key,
            _environment.EnvironmentName,
            oldValue,
            flag.IsEnabled,
            updatedBy ?? "unknown");

        return MapToDto(flag);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var flag = await _repository.GetByKeyAndEnvironmentAsync(
            key.ToLowerInvariant(),
            _environment.EnvironmentName,
            cancellationToken,
            track: true);

        if (flag is null)
        {
            return false;
        }

        _repository.Delete(flag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        InvalidateCache(key);

        _logger.LogInformation(
            "Feature flag deleted: Key={Key}, Environment={Environment}",
            key,
            _environment.EnvironmentName);

        return true;
    }

    private void InvalidateCache(string key)
    {
        var cacheKey = CacheKeyUtils.BuildFeatureFlagKey(_environment.EnvironmentName, key.ToLowerInvariant());
        _cache.Remove(cacheKey);
        _logger.LogDebug("Cache invalidated for feature flag: {Key}", key);
    }

    private static FeatureFlagDto MapToDto(FeatureFlag flag)
    {
        return new FeatureFlagDto(
            flag.Id,
            flag.Key,
            flag.Environment,
            flag.IsEnabled,
            flag.Description,
            flag.IsCritical,
            flag.LastUpdatedBy,
            flag.LastUpdatedAt,
            flag.Metadata);
    }
}
