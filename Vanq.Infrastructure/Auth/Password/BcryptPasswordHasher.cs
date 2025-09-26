using Vanq.Application.Abstractions.Auth;

namespace Vanq.Infrastructure.Auth.Password;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password);
    }

    public bool Verify(string hash, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
    }
}
