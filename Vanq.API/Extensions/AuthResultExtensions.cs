using System;
using Microsoft.AspNetCore.Http;
using Vanq.Application.Abstractions.Auth;

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
}
