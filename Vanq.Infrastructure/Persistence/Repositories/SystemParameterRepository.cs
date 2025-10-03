using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Repositories;

internal sealed class SystemParameterRepository : ISystemParameterRepository
{
    private readonly AppDbContext _dbContext;

    public SystemParameterRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemParameter?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken,
        bool track = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var query = _dbContext.SystemParameters.AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query
            .FirstOrDefaultAsync(
                p => p.Key == key.ToLowerInvariant(),
                cancellationToken);
    }

    public async Task<List<SystemParameter>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.SystemParameters
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SystemParameter>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        return await _dbContext.SystemParameters
            .AsNoTracking()
            .Where(p => p.Category == category)
            .OrderBy(p => p.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _dbContext.SystemParameters
            .AsNoTracking()
            .AnyAsync(p => p.Key == key.ToLowerInvariant(), cancellationToken);
    }

    public async Task AddAsync(SystemParameter parameter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        await _dbContext.SystemParameters.AddAsync(parameter, cancellationToken);
    }

    public void Update(SystemParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        _dbContext.SystemParameters.Update(parameter);
    }

    public void Delete(SystemParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        _dbContext.SystemParameters.Remove(parameter);
    }
}
