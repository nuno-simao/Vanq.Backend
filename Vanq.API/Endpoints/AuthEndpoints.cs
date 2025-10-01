using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanq.API.Extensions;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Contracts.Auth;
using Vanq.Shared.Security;

namespace Vanq.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithSummary("Registers a new user")
            .Produces<AuthResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithSummary("Authenticates a user")
            .Produces<AuthResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithSummary("Rotates the refresh token and returns a new access token")
            .Produces<AuthResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithSummary("Revokes the supplied refresh token")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapGet("/me", MeAsync)
            .WithSummary("Returns information about the current user")
            .Produces<CurrentUserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        group.MapRolesEndpoints();
        group.MapPermissionsEndpoints();
        group.MapUserRoleEndpoints();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterUserDto dto,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(dto, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] AuthRequestDto dto,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(dto, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshTokenRequestDto dto,
        IAuthRefreshService refreshService,
        CancellationToken cancellationToken)
    {
        var result = await refreshService.RefreshAsync(dto, cancellationToken);
        return result.ToHttpResult();
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] RefreshTokenRequestDto dto,
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var userId))
        {
            return Results.Unauthorized();
        }
        var result = await authService.LogoutAsync(userId, dto.RefreshToken, cancellationToken);
        return result.ToHttpResult(
            onSuccess: _ => Results.Ok(),
            onFailureOverride: failure => failure.Error switch
            {
                AuthError.InvalidRefreshToken => Results.BadRequest(new { error = failure.Message ?? "Invalid refresh token" }),
                _ => failure.ToFailureHttpResult()
            });
    }

    private static IResult MeAsync(ClaimsPrincipal principal, IAuthService authService)
    {
        var result = authService.GetCurrentUser(principal);
        return result.ToHttpResult();
    }
}
