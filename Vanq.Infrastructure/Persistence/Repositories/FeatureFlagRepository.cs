using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Repositories;

internal sealed class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly AppDbContext _dbContext;

    public FeatureFlagRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeatureFlag?> GetByKeyAndEnvironmentAsync(
        string key,
        string environment,
        CancellationToken cancellationToken,
        bool track = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        var query = _dbContext.FeatureFlags.AsQueryable();
        
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query
            .FirstOrDefaultAsync(
                f => f.Key == key.ToLowerInvariant() && f.Environment == environment,
                cancellationToken);
    }

    public async Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags
            .AsNoTracking()
            .OrderBy(f => f.Key)
            .ThenBy(f => f.Environment)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FeatureFlag>> GetByEnvironmentAsync(
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        return await _dbContext.FeatureFlags
            .AsNoTracking()
            .Where(f => f.Environment == environment)
            .OrderBy(f => f.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAndEnvironmentAsync(
        string key,
        string environment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        return await _dbContext.FeatureFlags
            .AsNoTracking()
            .AnyAsync(
                f => f.Key == key.ToLowerInvariant() && f.Environment == environment,
                cancellationToken);
    }

    public async Task AddAsync(FeatureFlag featureFlag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(featureFlag);
        await _dbContext.FeatureFlags.AddAsync(featureFlag, cancellationToken);
    }

    public void Update(FeatureFlag featureFlag)
    {
        ArgumentNullException.ThrowIfNull(featureFlag);
        _dbContext.FeatureFlags.Update(featureFlag);
    }

    public void Delete(FeatureFlag featureFlag)
    {
        ArgumentNullException.ThrowIfNull(featureFlag);
        _dbContext.FeatureFlags.Remove(featureFlag);
    }
}
