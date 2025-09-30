using Vanq.Domain.Entities;

namespace Vanq.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken, bool track = false);

    Task<RefreshToken?> GetByUserAndHashAsync(Guid userId, string tokenHash, CancellationToken cancellationToken, bool track = false);

    void Update(RefreshToken token);
}
