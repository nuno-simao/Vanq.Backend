using System.Security.Claims;
using Vanq.Application.Contracts.Auth;

namespace Vanq.Application.Abstractions.Auth;

public interface IAuthService
{
    Task<AuthResult<AuthResponseDto>> RegisterAsync(RegisterUserDto request, CancellationToken cancellationToken);

    Task<AuthResult<AuthResponseDto>> LoginAsync(AuthRequestDto request, CancellationToken cancellationToken);

    Task<AuthResult<bool>> LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken);

    AuthResult<CurrentUserDto> GetCurrentUser(ClaimsPrincipal principal);
}
