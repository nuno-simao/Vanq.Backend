namespace Vanq.Application.Abstractions.Auth;

public sealed record AuthResult<T>(bool IsSuccess, T? Value, AuthError? Error, string? Message = null)
{
    public static AuthResult<T> Success(T value) => new(true, value, null, null);

    public static AuthResult<T> Failure(AuthError error, string? message = null)
        => new(false, default, error, message);
}
