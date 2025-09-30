using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Auth.Jwt;

namespace Vanq.Infrastructure.Auth.Tokens;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOptions<JwtOptions> jwtOptions,
        IDateTimeProvider clock)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _jwtOptions = jwtOptions.Value;
        _clock = clock;
    }

    public async Task<(string PlainRefreshToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, string securityStamp, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var (plain, hash, expires) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var entity = RefreshToken.Issue(userId, hash, now, expires, securityStamp);

        await _refreshTokenRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (plain, expires);
    }

    public async Task<(string NewPlainRefreshToken, DateTime ExpiresAtUtc, Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);

        var token = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken, track: true);

        if (token is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (!token.IsActive(now))
        {
            throw new UnauthorizedAccessException("Refresh token expired or revoked");
        }

        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
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

        await _refreshTokenRepository.AddAsync(replacement, cancellationToken);
        _refreshTokenRepository.Update(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (newPlain, newExpires, user.Id, user.SecurityStamp);
    }

    public async Task RevokeAsync(Guid userId, string plainRefreshToken, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);

        var token = await _refreshTokenRepository.GetByUserAndHashAsync(userId, hash, cancellationToken, track: true);

        if (token is null)
        {
            return;
        }

        token.Revoke(nowUtc: now);
        _refreshTokenRepository.Update(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
