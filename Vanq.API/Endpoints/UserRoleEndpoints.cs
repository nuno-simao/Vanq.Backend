using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanq.API.Authorization;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Application.Contracts.Rbac;
using Vanq.Shared.Security;

namespace Vanq.API.Endpoints;

public static class UserRoleEndpoints
{
    public static RouteGroupBuilder MapUserRoleEndpoints(this RouteGroupBuilder group)
    {
        group = group.MapGroup("/users/{userId:guid}/roles")
            .WithTags("UserRoles")
            .RequireAuthorization();

        group.MapPost("/", AssignRoleToUserAsync)
            .WithSummary("Assigns a role to the target user")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("rbac:user:role:assign");

        group.MapDelete("/{roleId:guid}", RevokeRoleFromUserAsync)
            .WithSummary("Revokes a role from the target user")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("rbac:user:role:revoke");

        return group;
    }

    private static async Task<IResult> AssignRoleToUserAsync(
        Guid userId,
        [FromBody] AssignUserRoleRequest request,
        ClaimsPrincipal principal,
        IUserRoleService userRoleService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            await userRoleService.AssignRoleAsync(userId, request.RoleId, executorId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> RevokeRoleFromUserAsync(
        Guid userId,
        Guid roleId,
        ClaimsPrincipal principal,
        IUserRoleService userRoleService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            await userRoleService.RevokeRoleAsync(userId, roleId, executorId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }
}
