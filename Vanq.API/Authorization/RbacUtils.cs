using Vanq.Application.Abstractions.Rbac;

namespace Vanq.API.Authorization;

public static class RbacUtils
{
    internal static IResult HandleRbacException(this Exception exception)
    {
        return exception switch
        {
            RbacFeatureDisabledException => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
            UnauthorizedAccessException => Results.StatusCode(StatusCodes.Status403Forbidden),
            KeyNotFoundException => Results.NotFound(),
            InvalidOperationException invalid => Results.BadRequest(new { error = invalid.Message }),
            ArgumentException argument => Results.BadRequest(new { error = argument.Message }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}