using System.Security.Claims;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Contracts.Auth;
using Vanq.Domain.Entities;
using Vanq.Shared.Security;

namespace Vanq.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IDateTimeProvider clock)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _clock = clock;
    }

    public async Task<AuthResult<AuthResponseDto>> RegisterAsync(RegisterUserDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.EmailAlreadyInUse, "Email already registered");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(normalizedEmail, passwordHash, _clock.UtcNow);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);
        var (refreshToken, _) = await _refreshTokenService.IssueAsync(user.Id, user.SecurityStamp, cancellationToken);

        return AuthResult<AuthResponseDto>.Success(new AuthResponseDto(accessToken, refreshToken, expiresAtUtc));
    }

    public async Task<AuthResult<AuthResponseDto>> LoginAsync(AuthRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidCredentials);
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.UserInactive);
        }

        var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);
        var (refreshToken, _) = await _refreshTokenService.IssueAsync(user.Id, user.SecurityStamp, cancellationToken);

        return AuthResult<AuthResponseDto>.Success(new AuthResponseDto(accessToken, refreshToken, expiresAtUtc));
    }

    public async Task<AuthResult<bool>> LogoutAsync(Guid userId, string refreshToken, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return AuthResult<bool>.Failure(AuthError.MissingUserContext, "Invalid user identifier");
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult<bool>.Failure(AuthError.InvalidRefreshToken, "Refresh token is required");
        }

        await _refreshTokenService.RevokeAsync(userId, refreshToken, cancellationToken);
        return AuthResult<bool>.Success(true);
    }

    public AuthResult<CurrentUserDto> GetCurrentUser(ClaimsPrincipal principal)
    {
        if (!principal.TryGetUserContext(out var userId, out var email))
        {
            return AuthResult<CurrentUserDto>.Failure(AuthError.MissingUserContext, "User information is not available");
        }

    return AuthResult<CurrentUserDto>.Success(new CurrentUserDto(userId, email!));
    }
}
