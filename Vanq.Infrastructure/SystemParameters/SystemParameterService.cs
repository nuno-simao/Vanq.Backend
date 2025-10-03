using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.SystemParameters;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Contracts.SystemParameters;
using Vanq.Domain.Entities;
using Vanq.Shared;

namespace Vanq.Infrastructure.SystemParameters;

internal sealed class SystemParameterService : ISystemParameterService
{
    private const string MaskedValue = "***MASKED***";
    private const string CacheKeyPrefix = "system-param";

    private readonly ISystemParameterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SystemParameterService> _logger;
    private readonly int _cacheDurationSeconds;

    public SystemParameterService(
        ISystemParameterRepository repository,
        IUnitOfWork unitOfWork,
        IMemoryCache cache,
        IDateTimeProvider clock,
        IConfiguration configuration,
        ILogger<SystemParameterService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _clock = clock;
        _logger = logger;
        _cacheDurationSeconds = configuration.GetValue<int>("SystemParameters:CacheDurationSeconds", 60);
    }

    public async Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedKey = key.ToLowerInvariant();
        var cacheKey = BuildCacheKey(normalizedKey);

        // Try cache first
        if (_cache.TryGetValue<T>(cacheKey, out var cachedValue))
        {
            _logger.LogDebug("System parameter cache hit: {Key}", normalizedKey);
            return cachedValue;
        }

        // Cache miss - query database
        try
        {
            var parameter = await _repository.GetByKeyAsync(normalizedKey, cancellationToken);

            if (parameter is null)
            {
                _logger.LogWarning("System parameter not found: {Key}", normalizedKey);
                return default;
            }

            var convertedValue = SystemParameterTypeConverter.ConvertTo<T>(parameter.Value, parameter.Type);

            // Cache the result
            _cache.Set(cacheKey, convertedValue, TimeSpan.FromSeconds(_cacheDurationSeconds));

            _logger.LogDebug(
                "System parameter loaded from database: Key={Key}, Type={Type}",
                normalizedKey,
                parameter.Type);

            return convertedValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading system parameter '{Key}' from database. Returning default.",
                normalizedKey);
            return default;
        }
    }

    public async Task<T> GetValueOrDefaultAsync<T>(
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await GetValueAsync<T>(key, cancellationToken);

        if (result is null)
        {
            _logger.LogDebug(
                "System parameter not found or null, using default: Key={Key}, Default={Default}",
                key,
                defaultValue);
            return defaultValue;
        }

        return result;
    }

    public async Task<SystemParameterDto?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var parameter = await _repository.GetByKeyAsync(
            key.ToLowerInvariant(),
            cancellationToken);

        return parameter is null ? null : MapToDto(parameter);
    }

    public async Task<List<SystemParameterDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var parameters = await _repository.GetAllAsync(cancellationToken);
        return parameters.Select(MapToDto).ToList();
    }

    public async Task<List<SystemParameterDto>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var parameters = await _repository.GetByCategoryAsync(category, cancellationToken);
        return parameters.Select(MapToDto).ToList();
    }

    public async Task<SystemParameterDto> CreateAsync(
        CreateSystemParameterRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check if already exists
        var exists = await _repository.ExistsByKeyAsync(request.Key, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"System parameter with key '{request.Key}' already exists.");
        }

        // Validate value can be converted to specified type
        if (!SystemParameterTypeConverter.CanConvert(request.Value, request.Type))
        {
            throw new ArgumentException(
                $"Value '{request.Value}' cannot be converted to type '{request.Type}'.",
                nameof(request.Value));
        }

        var parameter = SystemParameter.Create(
            key: request.Key,
            value: request.Value,
            type: request.Type,
            category: request.Category,
            isSensitive: request.IsSensitive,
            createdBy: createdBy,
            nowUtc: _clock.UtcNow,
            reason: request.Reason,
            metadata: request.Metadata);

        await _repository.AddAsync(parameter, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache for this parameter
        InvalidateCache(request.Key);

        _logger.LogInformation(
            "System parameter created: Key={Key}, Type={Type}, IsSensitive={IsSensitive}, CreatedBy={CreatedBy}",
            request.Key,
            request.Type,
            request.IsSensitive,
            createdBy ?? "unknown");

        return MapToDto(parameter);
    }

    public async Task<SystemParameterDto?> UpdateAsync(
        string key,
        UpdateSystemParameterRequest request,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);

        var parameter = await _repository.GetByKeyAsync(
            key.ToLowerInvariant(),
            cancellationToken,
            track: true);

        if (parameter is null)
        {
            _logger.LogWarning("System parameter not found for update: {Key}", key);
            return null;
        }

        // Validate value can be converted to parameter's type
        if (!SystemParameterTypeConverter.CanConvert(request.Value, parameter.Type))
        {
            throw new ArgumentException(
                $"Value '{request.Value}' cannot be converted to type '{parameter.Type}'.",
                nameof(request.Value));
        }

        var oldValue = parameter.IsSensitive ? MaskedValue : parameter.Value;

        parameter.Update(
            value: request.Value,
            updatedBy: updatedBy,
            nowUtc: _clock.UtcNow,
            reason: request.Reason,
            metadata: request.Metadata);

        _repository.Update(parameter);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        InvalidateCache(key);

        _logger.LogInformation(
            "System parameter updated: Key={Key}, OldValue={OldValue}, NewValue={NewValue}, UpdatedBy={UpdatedBy}, Reason={Reason}",
            key,
            oldValue,
            parameter.IsSensitive ? MaskedValue : request.Value,
            updatedBy ?? "unknown",
            request.Reason ?? "not provided");

        return MapToDto(parameter);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var parameter = await _repository.GetByKeyAsync(
            key.ToLowerInvariant(),
            cancellationToken,
            track: true);

        if (parameter is null)
        {
            _logger.LogWarning("System parameter not found for deletion: {Key}", key);
            return false;
        }

        _repository.Delete(parameter);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        InvalidateCache(key);

        _logger.LogInformation(
            "System parameter deleted: Key={Key}, Type={Type}",
            key,
            parameter.Type);

        return true;
    }

    private void InvalidateCache(string key)
    {
        var normalizedKey = key.ToLowerInvariant();
        var cacheKey = BuildCacheKey(normalizedKey);
        _cache.Remove(cacheKey);

        _logger.LogDebug("System parameter cache invalidated: {Key}", normalizedKey);
    }

    private static string BuildCacheKey(string key) => $"{CacheKeyPrefix}:{key}";

    private static SystemParameterDto MapToDto(SystemParameter parameter)
    {
        return new SystemParameterDto(
            parameter.Id,
            parameter.Key,
            parameter.IsSensitive ? MaskedValue : parameter.Value,
            parameter.Type,
            parameter.Category,
            parameter.IsSensitive,
            parameter.LastUpdatedBy,
            parameter.LastUpdatedAt,
            parameter.Reason,
            parameter.Metadata);
    }
}
