using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Vanq.API.Authorization;
using Vanq.Application.Abstractions.SystemParameters;
using Vanq.Application.Contracts.SystemParameters;
using Vanq.Shared.Security;

namespace Vanq.API.Endpoints;

public static class SystemParametersEndpoints
{
    public static RouteGroupBuilder MapSystemParametersEndpoints(this RouteGroupBuilder group)
    {
        group = group.MapGroup("/admin/system-params")
            .WithTags("System Parameters")
            .RequireAuthorization();

        group.MapGet("/", GetAllParametersAsync)
            .WithSummary("Lists all system parameters")
            .WithDescription("Returns all system parameters with sensitive values masked. Requires admin role.")
            .Produces<List<SystemParameterDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:params:read");

        group.MapGet("/category/{category}", GetParametersByCategoryAsync)
            .WithSummary("Lists system parameters by category")
            .WithDescription("Returns all system parameters in a specific category with sensitive values masked.")
            .Produces<List<SystemParameterDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:params:read");

        group.MapGet("/{key}", GetParameterByKeyAsync)
            .WithSummary("Gets a specific system parameter")
            .WithDescription("Returns a system parameter by key. Sensitive values are masked.")
            .Produces<SystemParameterDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:params:read");

        group.MapPost("/", CreateParameterAsync)
            .WithSummary("Creates a new system parameter")
            .WithDescription("Creates a new system parameter with validation of key format and type conversion. Requires admin role.")
            .Produces<SystemParameterDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict)
            .RequirePermission("system:params:write");

        group.MapPut("/{key}", UpdateParameterAsync)
            .WithSummary("Updates a system parameter")
            .WithDescription("Updates an existing system parameter value. Invalidates cache and logs the change.")
            .Produces<SystemParameterDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:params:write");

        group.MapDelete("/{key}", DeleteParameterAsync)
            .WithSummary("Deletes a system parameter")
            .WithDescription("Deletes a system parameter and invalidates its cache.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:params:write");

        return group;
    }

    private static async Task<IResult> GetAllParametersAsync(
        ISystemParameterService service,
        CancellationToken cancellationToken)
    {
        var parameters = await service.GetAllAsync(cancellationToken);
        return Results.Ok(parameters);
    }

    private static async Task<IResult> GetParametersByCategoryAsync(
        string category,
        ISystemParameterService service,
        CancellationToken cancellationToken)
    {
        var parameters = await service.GetByCategoryAsync(category, cancellationToken);
        return Results.Ok(parameters);
    }

    private static async Task<IResult> GetParameterByKeyAsync(
        string key,
        ISystemParameterService service,
        CancellationToken cancellationToken)
    {
        var parameter = await service.GetByKeyAsync(key, cancellationToken);
        return parameter is null ? Results.NotFound() : Results.Ok(parameter);
    }

    private static async Task<IResult> CreateParameterAsync(
        CreateSystemParameterRequest request,
        ClaimsPrincipal principal,
        ISystemParameterService service,
        CancellationToken cancellationToken)
    {
        try
        {
            principal.TryGetUserContext(out _, out var email);
            var parameter = await service.CreateAsync(request, email, cancellationToken);
            return Results.Created($"/admin/system-params/{parameter.Key}", parameter);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateParameterAsync(
        string key,
        UpdateSystemParameterRequest request,
        ClaimsPrincipal principal,
        ISystemParameterService service,
        CancellationToken cancellationToken)
    {
        try
        {
            principal.TryGetUserContext(out _, out var email);
            var parameter = await service.UpdateAsync(key, request, email, cancellationToken);

            if (parameter is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(parameter);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteParameterAsync(
        string key,
        ISystemParameterService service,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(key, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
