using System;
using Microsoft.AspNetCore.Http;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.FeatureFlags;

namespace Vanq.API.Extensions;

internal static class AuthResultExtensions
{
    public static IResult ToHttpResult<T>(
        this AuthResult<T> result,
        Func<T, IResult>? onSuccess = null,
        Func<AuthResult<T>, IResult>? onFailureOverride = null)
    {
        if (result.IsSuccess)
        {
            if (onSuccess is not null)
            {
                return onSuccess(result.Value!);
            }

            if (result.Value is null)
            {
                return Results.Ok();
            }

            return Results.Ok(result.Value);
        }

        if (onFailureOverride is not null)
        {
            return onFailureOverride(result);
        }

        return result.ToFailureHttpResult();
    }

    public static IResult ToFailureHttpResult<T>(this AuthResult<T> result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot translate a successful result as failure.");
        }

        // Legacy fallback - will be replaced by middleware check for feature flag
        return result.Error switch
        {
            AuthError.EmailAlreadyInUse => Results.BadRequest(new { error = result.Message ?? "Email already registered" }),
            AuthError.InvalidCredentials => Results.Unauthorized(),
            AuthError.UserInactive => Results.Forbid(),
            AuthError.MissingUserContext => Results.Unauthorized(),
            AuthError.InvalidRefreshToken => Results.Unauthorized(),
            _ => Results.BadRequest(new { error = result.Message ?? "Authentication failed" })
        };
    }

    /// <summary>
    /// Converts AuthResult to HTTP result with Problem Details support.
    /// Checks feature flag to determine response format.
    /// </summary>
    public static async Task<IResult> ToHttpResultAsync<T>(
        this AuthResult<T> result,
        HttpContext httpContext,
        IFeatureFlagService featureFlagService,
        Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            if (onSuccess is not null)
            {
                return onSuccess(result.Value!);
            }

            if (result.Value is null)
            {
                return Results.Ok();
            }

            return Results.Ok(result.Value);
        }

        // Check if Problem Details is enabled
        var isProblemDetailsEnabled = await featureFlagService.IsEnabledAsync("problem-details-enabled");

        if (isProblemDetailsEnabled && result.Error.HasValue)
        {
            var problemDetails = result.Error.Value.ToProblemDetails(result.Message, httpContext);
            return Results.Json(
                problemDetails,
                contentType: "application/problem+json",
                statusCode: problemDetails.Status
            );
        }

        // Fallback to legacy format
        return result.ToFailureHttpResult();
    }
}
