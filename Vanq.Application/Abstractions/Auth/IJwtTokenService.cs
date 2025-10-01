namespace Vanq.Application.Abstractions.Auth;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(
        Guid userId,
        string email,
        string securityStamp,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string rolesSecurityStamp);
}
