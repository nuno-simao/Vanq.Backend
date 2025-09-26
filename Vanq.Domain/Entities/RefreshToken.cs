namespace Vanq.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string SecurityStampSnapshot { get; private set; } = null!;

    private RefreshToken() { }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTime created, DateTime expires, string securityStampSnapshot)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = created;
        ExpiresAt = expires;
        SecurityStampSnapshot = securityStampSnapshot;
    }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime nowUtc, DateTime expiresAt, string securityStampSnapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStampSnapshot);

        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, nowUtc, expiresAt, securityStampSnapshot);
    }

    public bool IsActive(DateTime nowUtc) => RevokedAt is null && nowUtc <= ExpiresAt;

    public void Revoke(string? replacedBy = null, DateTime? nowUtc = null)
    {
        if (RevokedAt is not null) return;

        RevokedAt = nowUtc ?? DateTime.UtcNow;
        ReplacedByTokenHash = replacedBy;
    }
}
