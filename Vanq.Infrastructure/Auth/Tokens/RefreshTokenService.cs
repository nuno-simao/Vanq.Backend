using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Auth.Jwt;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.Auth.Tokens;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions, IDateTimeProvider clock)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _clock = clock;
    }

    public async Task<(string PlainRefreshToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, string securityStamp, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var (plain, hash, expires) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var entity = RefreshToken.Issue(userId, hash, now, expires, securityStamp);

        await _dbContext.RefreshTokens.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (plain, expires);
    }

    public async Task<(string NewPlainRefreshToken, DateTime ExpiresAtUtc, Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);

        var token = await _dbContext.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (!token.IsActive(now))
        {
            throw new UnauthorizedAccessException("Refresh token expired or revoked");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        if (!string.Equals(user.SecurityStamp, token.SecurityStampSnapshot, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Refresh token stale");
        }

        var (newPlain, newHash, newExpires) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var replacement = RefreshToken.Issue(user.Id, newHash, now, newExpires, user.SecurityStamp);
        token.Revoke(replacedBy: newHash, nowUtc: now);

        await _dbContext.RefreshTokens.AddAsync(replacement, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (newPlain, newExpires, user.Id, user.SecurityStamp);
    }

    public async Task RevokeAsync(Guid userId, string plainRefreshToken, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);

        var token = await _dbContext.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == hash, cancellationToken);

        if (token is null)
        {
            return;
        }

        token.Revoke(nowUtc: now);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
