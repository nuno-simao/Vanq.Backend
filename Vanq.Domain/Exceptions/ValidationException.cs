namespace Vanq.Domain.Exceptions;

/// <summary>
/// Exception thrown when domain validation fails.
/// </summary>
public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(
        string message,
        IDictionary<string, string[]>? errors = null,
        string errorCode = "VALIDATION_ERROR")
        : base(message, errorCode, httpStatusCode: 400)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public ValidationException(
        string field,
        string errorMessage,
        string errorCode = "VALIDATION_ERROR")
        : base($"Validation failed for field '{field}'", errorCode, httpStatusCode: 400)
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = new[] { errorMessage }
        };
    }
}
