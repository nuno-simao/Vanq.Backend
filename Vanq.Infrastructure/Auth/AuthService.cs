using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Configuration;
using Vanq.Application.Contracts.Auth;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Rbac;
using Vanq.Shared;
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
    private readonly IRoleRepository _roleRepository;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly RbacOptions _rbacOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IDateTimeProvider clock,
        IRoleRepository roleRepository,
        IFeatureFlagService featureFlagService,
        IOptions<RbacOptions> rbacOptions,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _clock = clock;
        _roleRepository = roleRepository;
        _featureFlagService = featureFlagService;
        _rbacOptions = rbacOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResult<AuthResponseDto>> RegisterAsync(RegisterUserDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = StringNormalizationUtils.NormalizeEmail(request.Email);

        var emailExists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return AuthResult<AuthResponseDto>.Failure(AuthError.EmailAlreadyInUse, "Email already registered");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(normalizedEmail, passwordHash, _clock.UtcNow);

        if (await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            await AssignDefaultRoleIfNeededAsync(user, cancellationToken).ConfigureAwait(false);
        }

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var enrichedUser = await _userRepository.GetByIdWithRolesAsync(user.Id, cancellationToken).ConfigureAwait(false) ?? user;
    var (roles, permissions, rolesStamp) = RbacTokenPayloadBuilder.Build(enrichedUser);
        var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp, roles, permissions, rolesStamp);
        var (refreshToken, _) = await _refreshTokenService.IssueAsync(user.Id, user.SecurityStamp, cancellationToken);

        return AuthResult<AuthResponseDto>.Success(new AuthResponseDto(accessToken, refreshToken, expiresAtUtc));
    }

    public async Task<AuthResult<AuthResponseDto>> LoginAsync(AuthRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = StringNormalizationUtils.NormalizeEmail(request.Email);
        var user = await _userRepository.GetByEmailWithRolesAsync(normalizedEmail, cancellationToken);

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

        if (await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken) && !user.HasAnyActiveRole())
        {
            await AssignDefaultRoleToPersistedUserAsync(user, cancellationToken).ConfigureAwait(false);
            user = await _userRepository.GetByIdWithRolesAsync(user.Id, cancellationToken).ConfigureAwait(false) ?? user;
        }

    var (roles, permissions, rolesStamp) = RbacTokenPayloadBuilder.Build(user);
        var (accessToken, expiresAtUtc) = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp, roles, permissions, rolesStamp);
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

        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = principal.FindAll("permission").Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return AuthResult<CurrentUserDto>.Success(new CurrentUserDto(userId, email!, roles, permissions));
    }

    private async Task AssignDefaultRoleIfNeededAsync(User user, CancellationToken cancellationToken)
    {
        if (user.HasAnyActiveRole())
        {
            return;
        }

        var defaultRole = await GetDefaultRoleAsync(cancellationToken).ConfigureAwait(false);
        if (defaultRole is null)
        {
            _logger.LogWarning("Default role '{DefaultRole}' not found. Skipping automatic assignment.", _rbacOptions.DefaultRole);
            return;
        }

        if (user.HasActiveRole(defaultRole.Id))
        {
            return;
        }

        var timestamp = _clock.GetUtcDateTimeOffset();
        user.AssignRole(defaultRole.Id, Guid.Empty, timestamp);
    }

    private async Task AssignDefaultRoleToPersistedUserAsync(User user, CancellationToken cancellationToken)
    {
        var defaultRole = await GetDefaultRoleAsync(cancellationToken).ConfigureAwait(false);
        if (defaultRole is null)
        {
            _logger.LogWarning("Default role '{DefaultRole}' not found. User {UserId} remains without roles.", _rbacOptions.DefaultRole, user.Id);
            return;
        }

        if (user.HasActiveRole(defaultRole.Id))
        {
            return;
        }

        var timestamp = _clock.GetUtcDateTimeOffset();
        user.AssignRole(defaultRole.Id, Guid.Empty, timestamp);
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Role?> GetDefaultRoleAsync(CancellationToken cancellationToken)
    {
        if (!await _featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_rbacOptions.DefaultRole))
        {
            return null;
        }

        var normalized = StringNormalizationUtils.NormalizeName(_rbacOptions.DefaultRole);
        return await _roleRepository.GetByNameWithPermissionsAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

}
