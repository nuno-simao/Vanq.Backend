namespace Vanq.Domain.Exceptions;

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(
        string message = "Authentication failed",
        string errorCode = "UNAUTHORIZED")
        : base(message, errorCode, httpStatusCode: 401)
    {
    }
}
