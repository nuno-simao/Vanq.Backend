using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Contracts.Auth;
using Vanq.Infrastructure.Rbac;

namespace Vanq.Infrastructure.Auth;

public sealed class AuthRefreshService : IAuthRefreshService
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository _userRepository;

    public AuthRefreshService(
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository)
    {
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
    }

    public async Task<AuthResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var (newRefreshToken, _, userId, securityStamp) = await _refreshTokenService.ValidateAndRotateAsync(request.RefreshToken, cancellationToken);
            var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);

            if (user is null)
            {
                return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidRefreshToken, "User not found");
            }

            var (roles, permissions, rolesStamp) = RbacTokenPayloadBuilder.Build(user);
            var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, securityStamp, roles, permissions, rolesStamp);

            return AuthResult<AuthResponseDto>.Success(new AuthResponseDto(accessToken, newRefreshToken, expiresAtUtc));
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidRefreshToken, ex.Message);
        }
    }
}
