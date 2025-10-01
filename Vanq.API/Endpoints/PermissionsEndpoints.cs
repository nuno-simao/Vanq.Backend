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

public static class PermissionsEndpoints
{
    public static RouteGroupBuilder MapPermissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/permissions")
            .WithTags("Permissions")
            .RequireAuthorization();

        group.MapGet("/", GetPermissionsAsync)
            .WithSummary("Lists all permissions")
            .Produces<List<PermissionDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("rbac:permission:read");

        group.MapPost("/", CreatePermissionAsync)
            .WithSummary("Creates a new permission")
            .Produces<PermissionDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("rbac:permission:create");

        group.MapPatch("/{permissionId:guid}", UpdatePermissionAsync)
            .WithSummary("Updates permission metadata")
            .Produces<PermissionDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("rbac:permission:update");

        group.MapDelete("/{permissionId:guid}", DeletePermissionAsync)
            .WithSummary("Deletes a permission")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("rbac:permission:delete");

        return group;
    }

    private static async Task<IResult> GetPermissionsAsync(
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var permissions = await permissionService.GetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(permissions);
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> CreatePermissionAsync(
        [FromBody] CreatePermissionRequest request,
        ClaimsPrincipal principal,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var permission = await permissionService.CreateAsync(request, executorId, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/auth/permissions/{permission.Id}", permission);
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> UpdatePermissionAsync(
        Guid permissionId,
        [FromBody] UpdatePermissionRequest request,
        ClaimsPrincipal principal,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var permission = await permissionService.UpdateAsync(permissionId, request, executorId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(permission);
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }

    private static async Task<IResult> DeletePermissionAsync(
        Guid permissionId,
        ClaimsPrincipal principal,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var executorId))
        {
            return Results.Unauthorized();
        }

        try
        {
            await permissionService.DeleteAsync(permissionId, executorId, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return ex.HandleRbacException();
        }
    }
}
