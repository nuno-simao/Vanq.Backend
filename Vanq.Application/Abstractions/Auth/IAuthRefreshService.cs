using Vanq.Application.Contracts.Auth;

namespace Vanq.Application.Abstractions.Auth;

public interface IAuthRefreshService
{
    Task<AuthResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken);
}
