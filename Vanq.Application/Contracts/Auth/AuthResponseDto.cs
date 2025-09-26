namespace Vanq.Application.Contracts.Auth;

public sealed record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, string TokenType = "Bearer");
