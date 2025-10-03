namespace Vanq.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(
        string resourceName,
        object resourceKey,
        string errorCode = "NOT_FOUND")
        : base($"{resourceName} with key '{resourceKey}' was not found", errorCode, httpStatusCode: 404)
    {
    }

    public NotFoundException(
        string message,
        string errorCode = "NOT_FOUND")
        : base(message, errorCode, httpStatusCode: 404)
    {
    }
}
