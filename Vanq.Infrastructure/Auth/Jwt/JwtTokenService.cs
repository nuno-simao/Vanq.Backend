using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vanq.Application.Abstractions.Auth;

namespace Vanq.Infrastructure.Auth.Jwt;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtOptions> options, ILogger<JwtTokenService> logger)
    {
        _options = options.Value;
        _signingKey = Encoding.UTF8.GetBytes(_options.SigningKey);
        _logger = logger;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(
        Guid userId,
        string email,
        string securityStamp,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        string rolesSecurityStamp)
    {
        try
        {
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_options.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Email, email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("security_stamp", securityStamp)
            };

            if (!string.IsNullOrWhiteSpace(rolesSecurityStamp))
            {
                claims.Add(new Claim("roles_stamp", rolesSecurityStamp));
            }

            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            foreach (var permission in permissions)
            {
                if (!string.IsNullOrWhiteSpace(permission))
                {
                    claims.Add(new Claim("permission", permission));
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                NotBefore = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_signingKey), SecurityAlgorithms.HmacSha256)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            _logger.LogDebug("JWT access token generated for user {UserId} with {RoleCount} roles and {PermissionCount} permissions",
                userId, roles.Count, permissions.Count);
            return (_tokenHandler.WriteToken(token), expires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate JWT access token for user {UserId}", userId);
            throw;
        }
    }
}
