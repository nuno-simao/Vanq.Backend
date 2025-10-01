using System;
using System.Collections.Generic;
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

public static class RolesEndpoints
{
    public static RouteGroupBuilder MapRolesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/roles")
            .WithTags("Roles")
            .RequireAuthorization();

        group.MapGet("/", GetRolesAsync)
            .WithSummary("Lists all roles")
            .Produces<List<RoleDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("rbac:role:read");

        group.MapPost("/", CreateRoleAsync)
            .WithSummary("Creates a new role")
            .Produces<RoleDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("rbac:role:create");

        group.MapPatch("/{roleId:guid}", UpdateRoleAsync)
            .WithSummary("Updates role details and permissions")
            .Produces<RoleDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("rbac:role:update");

        group.MapDelete("/{roleId:guid}", DeleteRoleAsync)
            .WithSummary("Deletes a role")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("rbac:role:delete");

        return group;
    }

    private static async Task<IResult> GetRolesAsync(
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        try
        {
            var roles = await roleService.GetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(roles);
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> CreateRoleAsync(
        [FromBody] CreateRoleRequest request,
        ClaimsPrincipal principal,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var role = await roleService.CreateAsync(request, executorId, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/auth/roles/{role.Id}", role);
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> UpdateRoleAsync(
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        ClaimsPrincipal principal,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var role = await roleService.UpdateAsync(roleId, request, executorId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(role);
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> DeleteRoleAsync(
        Guid roleId,
        ClaimsPrincipal principal,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            await roleService.DeleteAsync(roleId, executorId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }
}
