using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Application.Abstractions.Rbac;
using Vanq.Shared.Security;

namespace Vanq.API.Authorization;

internal sealed class PermissionEndpointFilter : IEndpointFilter
{
    private readonly string _requiredPermission;
    private readonly ILogger<PermissionEndpointFilter> _logger;

    public PermissionEndpointFilter(string requiredPermission, ILogger<PermissionEndpointFilter> logger)
    {
        _requiredPermission = requiredPermission;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var cancellationToken = httpContext.RequestAborted;
        var principal = httpContext.User;

        if (!principal.TryGetUserId(out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var featureFlagService = httpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        if (!await featureFlagService.IsEnabledAsync("rbac-enabled", cancellationToken))
        {
            return await next(context);
        }

        var permissionChecker = httpContext.RequestServices.GetRequiredService<IPermissionChecker>();

        try
        {
            await permissionChecker.EnsurePermissionAsync(userId, _requiredPermission, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogInformation(
                "Permission requirement failed. User={UserId}, Permission={Permission}",
                userId,
                _requiredPermission);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
