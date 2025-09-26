namespace Vanq.Application.Abstractions.Tokens;

public interface IRefreshTokenService
{
    Task<(string PlainRefreshToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);
    Task<(string NewPlainRefreshToken, DateTime ExpiresAtUtc, Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(Guid userId, string plainRefreshToken, CancellationToken cancellationToken);
}
