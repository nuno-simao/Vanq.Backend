using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Contracts.Auth;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Persistence;

namespace Vanq.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithSummary("Registers a new user");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithSummary("Authenticates a user");

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithSummary("Rotates the refresh token and returns a new access token");

        group.MapPost("/logout", LogoutAsync)
            .WithSummary("Revokes the supplied refresh token")
            .RequireAuthorization();

        group.MapGet("/me", MeAsync)
            .WithSummary("Returns information about the current user")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterUserDto dto,
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        if (await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            return Results.BadRequest(new { error = "Email already registered" });
        }

        var passwordHash = passwordHasher.Hash(dto.Password);
        var user = User.Create(normalizedEmail, passwordHash, DateTime.UtcNow);

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAtUtc) = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);
        var (refreshToken, _) = await refreshTokenService.IssueAsync(user.Id, user.SecurityStamp, cancellationToken);

        return Results.Ok(new AuthResponseDto(accessToken, refreshToken, expiresAtUtc));
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] AuthRequestDto dto,
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!passwordHasher.Verify(user.PasswordHash, dto.Password))
        {
            return Results.Unauthorized();
        }

        if (!user.IsActive)
        {
            return Results.Forbid();
        }

        var (accessToken, expiresAtUtc) = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);
        var (refreshToken, _) = await refreshTokenService.IssueAsync(user.Id, user.SecurityStamp, cancellationToken);

        return Results.Ok(new AuthResponseDto(accessToken, refreshToken, expiresAtUtc));
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshTokenRequestDto dto,
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var (newRefreshToken, refreshExpiresAt, userId, securityStamp) = await refreshTokenService.ValidateAndRotateAsync(dto.RefreshToken, cancellationToken);
            var user = await dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);

            var (accessToken, expiresAtUtc) = jwtTokenService.GenerateAccessToken(user.Id, user.Email, securityStamp);
            return Results.Ok(new AuthResponseDto(accessToken, newRefreshToken, expiresAtUtc));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] RefreshTokenRequestDto dto,
        ClaimsPrincipal principal,
        IRefreshTokenService refreshTokenService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null)
        {
            return Results.Unauthorized();
        }

        var userId = Guid.Parse(userIdClaim);
        await refreshTokenService.RevokeAsync(userId, dto.RefreshToken, cancellationToken);

        return Results.Ok();
    }

    private static IResult MeAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);

        if (userId is null || email is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new { Id = userId, Email = email });
    }
}
