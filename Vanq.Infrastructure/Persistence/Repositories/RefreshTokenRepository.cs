using System.Linq;
using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;

    public RefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _dbContext.RefreshTokens.AddAsync(token, cancellationToken).AsTask();
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken, bool track = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return await Query(track)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<RefreshToken?> GetByUserAndHashAsync(Guid userId, string tokenHash, CancellationToken cancellationToken, bool track = false)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return await Query(track)
            .FirstOrDefaultAsync(token => token.UserId == userId && token.TokenHash == tokenHash, cancellationToken);
    }

    public void Update(RefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        _dbContext.RefreshTokens.Update(token);
    }

    private IQueryable<RefreshToken> Query(bool track)
    {
        return track ? _dbContext.RefreshTokens.AsTracking() : _dbContext.RefreshTokens.AsNoTracking();
    }
}
