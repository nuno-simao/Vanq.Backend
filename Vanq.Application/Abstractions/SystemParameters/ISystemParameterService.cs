using Vanq.Application.Contracts.SystemParameters;

namespace Vanq.Application.Abstractions.SystemParameters;

public interface ISystemParameterService
{
    /// <summary>
    /// Gets a system parameter value by key with strong typing.
    /// Uses cache for performance.
    /// </summary>
    Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a system parameter value with a default fallback if not found.
    /// </summary>
    Task<T> GetValueOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a system parameter by key.
    /// </summary>
    Task<SystemParameterDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all system parameters (sensitive values masked).
    /// </summary>
    Task<List<SystemParameterDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets system parameters by category.
    /// </summary>
    Task<List<SystemParameterDto>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new system parameter.
    /// </summary>
    Task<SystemParameterDto> CreateAsync(
        CreateSystemParameterRequest request,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing system parameter and invalidates cache.
    /// </summary>
    Task<SystemParameterDto?> UpdateAsync(
        string key,
        UpdateSystemParameterRequest request,
        string? updatedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a system parameter.
    /// </summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
