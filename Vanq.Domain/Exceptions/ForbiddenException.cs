namespace Vanq.Domain.Exceptions;

/// <summary>
/// Exception thrown when authorization fails (user is authenticated but lacks permissions).
/// </summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(
        string message = "Access forbidden",
        string errorCode = "FORBIDDEN")
        : base(message, errorCode, httpStatusCode: 403)
    {
    }
}
