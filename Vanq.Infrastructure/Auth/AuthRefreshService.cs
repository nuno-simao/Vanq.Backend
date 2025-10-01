using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Contracts.Auth;
using Vanq.Infrastructure.Logging.Extensions;
using Vanq.Infrastructure.Rbac;

namespace Vanq.Infrastructure.Auth;

public sealed class AuthRefreshService : IAuthRefreshService
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthRefreshService> _logger;

    public AuthRefreshService(
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository,
        ILogger<AuthRefreshService> logger)
    {
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<AuthResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var (newRefreshToken, _, userId, securityStamp) = await _refreshTokenService.ValidateAndRotateAsync(request.RefreshToken, cancellationToken);
            var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);

            if (user is null)
            {
                _logger.LogAuthEvent("TokenRefresh", "failure", userId: userId, reason: "UserNotFound");
                return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidRefreshToken, "User not found");
            }

            var (roles, permissions, rolesStamp) = RbacTokenPayloadBuilder.Build(user);
            var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, securityStamp, roles, permissions, rolesStamp);

            _logger.LogAuthEvent("TokenRefresh", "success", userId: user.Id, email: user.Email);
            return AuthResult<AuthResponseDto>.Success(new AuthResponseDto(accessToken, newRefreshToken, expiresAtUtc));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogAuthEvent("TokenRefresh", "failure", reason: ex.Message);
            return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidRefreshToken, ex.Message);
        }
    }
}
