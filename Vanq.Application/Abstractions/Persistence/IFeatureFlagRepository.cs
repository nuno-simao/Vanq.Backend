using Vanq.Domain.Entities;

namespace Vanq.Application.Abstractions.Persistence;

public interface IFeatureFlagRepository
{
    Task<FeatureFlag?> GetByKeyAndEnvironmentAsync(
        string key, 
        string environment, 
        CancellationToken cancellationToken,
        bool track = false);

    Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken);

    Task<List<FeatureFlag>> GetByEnvironmentAsync(
        string environment, 
        CancellationToken cancellationToken);

    Task<bool> ExistsByKeyAndEnvironmentAsync(
        string key, 
        string environment, 
        CancellationToken cancellationToken);

    Task AddAsync(FeatureFlag featureFlag, CancellationToken cancellationToken);

    void Update(FeatureFlag featureFlag);

    void Delete(FeatureFlag featureFlag);
}
