using Vanq.Domain.Entities;

namespace Vanq.Application.Abstractions.Persistence;

public interface ISystemParameterRepository
{
    Task<SystemParameter?> GetByKeyAsync(string key, CancellationToken cancellationToken, bool track = false);

    Task<List<SystemParameter>> GetAllAsync(CancellationToken cancellationToken);

    Task<List<SystemParameter>> GetByCategoryAsync(string category, CancellationToken cancellationToken);

    Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken);

    Task AddAsync(SystemParameter parameter, CancellationToken cancellationToken);

    void Update(SystemParameter parameter);

    void Delete(SystemParameter parameter);
}
