namespace Vanq.Domain.Exceptions;

/// <summary>
/// Base exception for all domain-level exceptions.
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    public int HttpStatusCode { get; }

    protected DomainException(
        string message,
        string errorCode,
        int httpStatusCode = 400,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        HttpStatusCode = httpStatusCode;
    }
}
