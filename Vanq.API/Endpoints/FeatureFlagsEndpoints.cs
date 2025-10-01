using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanq.API.Authorization;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Contracts.FeatureFlags;
using Vanq.Shared.Security;

namespace Vanq.API.Endpoints;

public static class FeatureFlagsEndpoints
{
    public static RouteGroupBuilder MapFeatureFlagsEndpoints(this RouteGroupBuilder group)
    {
        group = group.MapGroup("/admin/feature-flags")
            .WithTags("Feature Flags")
            .RequireAuthorization();

        group.MapGet("/", GetAllFlagsAsync)
            .WithSummary("Lists all feature flags")
            .WithDescription("Returns all feature flags across all environments. Requires admin role.")
            .Produces<List<FeatureFlagDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:feature-flags:read");

        group.MapGet("/current", GetCurrentEnvironmentFlagsAsync)
            .WithSummary("Lists feature flags for current environment")
            .WithDescription("Returns all feature flags for the current environment. Requires admin role.")
            .Produces<List<FeatureFlagDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:feature-flags:read");

        group.MapGet("/{key}", GetFlagByKeyAsync)
            .WithSummary("Gets a specific feature flag")
            .WithDescription("Returns a feature flag by key for the current environment.")
            .Produces<FeatureFlagDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:feature-flags:read");

        group.MapPost("/", CreateFlagAsync)
            .WithSummary("Creates a new feature flag")
            .WithDescription("Creates a new feature flag. Requires admin role.")
            .Produces<FeatureFlagDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict)
            .RequirePermission("system:feature-flags:create");

        group.MapPut("/{key}", UpdateFlagAsync)
            .WithSummary("Updates a feature flag")
            .WithDescription("Updates an existing feature flag for the current environment. Invalidates cache.")
            .Produces<FeatureFlagDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:feature-flags:update");

        group.MapPost("/{key}/toggle", ToggleFlagAsync)
            .WithSummary("Toggles a feature flag")
            .WithDescription("Toggles a feature flag on/off for the current environment. Invalidates cache.")
            .Produces<FeatureFlagDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:feature-flags:update");

        group.MapDelete("/{key}", DeleteFlagAsync)
            .WithSummary("Deletes a feature flag")
            .WithDescription("Deletes a feature flag from the current environment.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .RequirePermission("system:feature-flags:delete");

        return group;
    }

    private static async Task<IResult> GetAllFlagsAsync(
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        var flags = await featureFlagService.GetAllAsync(cancellationToken);
        return Results.Ok(flags);
    }

    private static async Task<IResult> GetCurrentEnvironmentFlagsAsync(
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        var flags = await featureFlagService.GetByEnvironmentAsync(cancellationToken);
        return Results.Ok(flags);
    }

    private static async Task<IResult> GetFlagByKeyAsync(
        string key,
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        var flag = await featureFlagService.GetByKeyAsync(key, cancellationToken);
        
        if (flag is null)
        {
            return Results.NotFound(new { message = $"Feature flag '{key}' not found for current environment." });
        }

        return Results.Ok(flag);
    }

    private static async Task<IResult> CreateFlagAsync(
        [FromBody] CreateFeatureFlagDto request,
        ClaimsPrincipal principal,
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserContext(out _, out var email) || email is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var flag = await featureFlagService.CreateAsync(request, email, cancellationToken);
            return Results.Created($"/admin/feature-flags/{flag.Key}", flag);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateFlagAsync(
        string key,
        [FromBody] UpdateFeatureFlagDto request,
        ClaimsPrincipal principal,
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserContext(out _, out var email) || email is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var flag = await featureFlagService.UpdateAsync(key, request, email, cancellationToken);
            
            if (flag is null)
            {
                return Results.NotFound(new { message = $"Feature flag '{key}' not found for current environment." });
            }

            return Results.Ok(flag);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ToggleFlagAsync(
        string key,
        ClaimsPrincipal principal,
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserContext(out _, out var email) || email is null)
        {
            return Results.Unauthorized();
        }

        var flag = await featureFlagService.ToggleAsync(key, email, cancellationToken);
        
        if (flag is null)
        {
            return Results.NotFound(new { message = $"Feature flag '{key}' not found for current environment." });
        }

        return Results.Ok(flag);
    }

    private static async Task<IResult> DeleteFlagAsync(
        string key,
        IFeatureFlagService featureFlagService,
        CancellationToken cancellationToken)
    {
        var deleted = await featureFlagService.DeleteAsync(key, cancellationToken);
        
        if (!deleted)
        {
            return Results.NotFound(new { message = $"Feature flag '{key}' not found for current environment." });
        }

        return Results.NoContent();
    }
}
