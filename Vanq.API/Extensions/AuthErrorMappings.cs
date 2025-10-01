using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Vanq.API.ProblemDetails;
using Vanq.Application.Abstractions.Auth;

namespace Vanq.API.Extensions;

/// <summary>
/// Maps AuthError enum to Problem Details error codes and metadata.
/// </summary>
internal static class AuthErrorMappings
{
    private record ErrorMapping(
        string ErrorType,
        string Title,
        int Status,
        string ErrorCode
    );

    private static readonly Dictionary<AuthError, ErrorMapping> Mappings = new()
    {
        [AuthError.EmailAlreadyInUse] = new(
            "email-already-in-use",
            "Email Already In Use",
            StatusCodes.Status409Conflict,
            "EMAIL_ALREADY_IN_USE"
        ),
        [AuthError.InvalidCredentials] = new(
            "invalid-credentials",
            "Invalid Credentials",
            StatusCodes.Status401Unauthorized,
            "INVALID_CREDENTIALS"
        ),
        [AuthError.UserInactive] = new(
            "user-inactive",
            "User Inactive",
            StatusCodes.Status403Forbidden,
            "USER_INACTIVE"
        ),
        [AuthError.MissingUserContext] = new(
            "missing-user-context",
            "Missing User Context",
            StatusCodes.Status401Unauthorized,
            "MISSING_USER_CONTEXT"
        ),
        [AuthError.InvalidRefreshToken] = new(
            "invalid-refresh-token",
            "Invalid Refresh Token",
            StatusCodes.Status401Unauthorized,
            "INVALID_REFRESH_TOKEN"
        )
    };

    public static VanqProblemDetails ToProblemDetails(
        this AuthError error,
        string? message,
        HttpContext httpContext)
    {
        if (!Mappings.TryGetValue(error, out var mapping))
        {
            // Fallback for unmapped errors
            mapping = new ErrorMapping(
                "authentication-failed",
                "Authentication Failed",
                StatusCodes.Status400BadRequest,
                "AUTHENTICATION_FAILED"
            );
        }

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        return ProblemDetailsBuilder.CreateStandard(
            errorType: mapping.ErrorType,
            title: mapping.Title,
            status: mapping.Status,
            detail: message ?? mapping.Title,
            instance: httpContext.Request.Path,
            traceId: traceId,
            errorCode: mapping.ErrorCode
        );
    }
}
