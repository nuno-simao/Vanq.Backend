using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Auth.Jwt;
using Vanq.Infrastructure.Logging.Extensions;

namespace Vanq.Infrastructure.Auth.Tokens;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOptions<JwtOptions> jwtOptions,
        IDateTimeProvider clock,
        ILogger<RefreshTokenService> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _jwtOptions = jwtOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<(string PlainRefreshToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, string securityStamp, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var (plain, hash, expires) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var entity = RefreshToken.Issue(userId, hash, now, expires, securityStamp);

        await _refreshTokenRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Refresh token issued for user {UserId}, expires at {ExpiresAt}", userId, expires);
        return (plain, expires);
    }

    public async Task<(string NewPlainRefreshToken, DateTime ExpiresAtUtc, Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var now = _clock.UtcNow;
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);

        var token = await _refreshTokenRepository.GetByHashAsync(hash, cancellationToken, track: true);

        if (token is null)
        {
            _logger.LogWarning("Refresh token validation failed: token not found");
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (!token.IsActive(now))
        {
            _logger.LogWarning("Refresh token validation failed: token expired or revoked for user {UserId}", token.UserId);
            throw new UnauthorizedAccessException("Refresh token expired or revoked");
        }

        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Refresh token validation failed: user {UserId} not found or inactive", token.UserId);
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        if (!string.Equals(user.SecurityStamp, token.SecurityStampSnapshot, StringComparison.Ordinal))
        {
            _logger.LogSecurityEvent("RefreshTokenStale", "medium",
                details: "Security stamp mismatch detected",
                userId: user.Id);
            throw new UnauthorizedAccessException("Refresh token stale");
        }

        var (newPlain, newHash, newExpires) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var replacement = RefreshToken.Issue(user.Id, newHash, now, newExpires, user.SecurityStamp);
        token.Revoke(replacedBy: newHash, nowUtc: now);

        await _refreshTokenRepository.AddAsync(replacement, cancellationToken);
        _refreshTokenRepository.Update(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        stopwatch.Stop();
        _logger.LogPerformanceEvent("RefreshTokenRotation", stopwatch.ElapsedMilliseconds, threshold: 200);
        _logger.LogDebug("Refresh token rotated for user {UserId}", user.Id);
        return (newPlain, newExpires, user.Id, user.SecurityStamp);
    }

    public async Task RevokeAsync(Guid userId, string plainRefreshToken, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);

        var token = await _refreshTokenRepository.GetByUserAndHashAsync(userId, hash, cancellationToken, track: true);

        if (token is null)
        {
            _logger.LogDebug("Refresh token revocation skipped: token not found for user {UserId}", userId);
            return;
        }

        token.Revoke(nowUtc: now);
        _refreshTokenRepository.Update(token);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token revoked for user {UserId}", userId);
    }
}
