namespace Vanq.Domain.Exceptions;

/// <summary>
/// Exception thrown when a resource conflict occurs (e.g., duplicate email).
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(
        string message,
        string errorCode = "CONFLICT")
        : base(message, errorCode, httpStatusCode: 409)
    {
    }
}
