using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Contracts.Auth;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.Auth;

public sealed class AuthRefreshService : IAuthRefreshService
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AppDbContext _dbContext;

    public AuthRefreshService(
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        AppDbContext dbContext)
    {
        _refreshTokenService = refreshTokenService;
        _jwtTokenService = jwtTokenService;
        _dbContext = dbContext;
    }

    public async Task<AuthResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var (newRefreshToken, _, userId, securityStamp) = await _refreshTokenService.ValidateAndRotateAsync(request.RefreshToken, cancellationToken);
            var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user is null)
            {
                return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidRefreshToken, "User not found");
            }

            var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, securityStamp);

            return AuthResult<AuthResponseDto>.Success(new AuthResponseDto(accessToken, newRefreshToken, expiresAtUtc));
        }
        catch (UnauthorizedAccessException ex)
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidRefreshToken, ex.Message);
        }
    }
}
