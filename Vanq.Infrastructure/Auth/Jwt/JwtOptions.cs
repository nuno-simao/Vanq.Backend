namespace Vanq.Infrastructure.Auth.Jwt;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public string SigningKey { get; init; } = null!;
    public int AccessTokenMinutes { get; init; }
    public int RefreshTokenDays { get; init; }
}
