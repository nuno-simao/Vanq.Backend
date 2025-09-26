namespace Vanq.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public string SecurityStamp { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private User() { }

    private User(Guid id, string email, string passwordHash, string securityStamp, DateTime createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string passwordHash, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User(Guid.NewGuid(), normalizedEmail, passwordHash, Guid.NewGuid().ToString("N"), nowUtc);
    }

    public void SetPasswordHash(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);

        PasswordHash = newHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }
}
