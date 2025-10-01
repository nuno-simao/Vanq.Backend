namespace Vanq.API.ProblemDetails;

/// <summary>
/// Constants for Problem Details RFC 7807 implementation.
/// </summary>
public static class ProblemDetailsConstants
{
    /// <summary>
    /// Base URL for error documentation (type URIs).
    /// </summary>
    public const string BaseTypeUrl = "https://api.vanq.dev/errors";

    /// <summary>
    /// Extension keys for additional Problem Details data.
    /// </summary>
    public static class Extensions
    {
        public const string TraceId = "traceId";
        public const string Timestamp = "timestamp";
        public const string ErrorCode = "errorCode";
        public const string Errors = "errors";
    }

    /// <summary>
    /// Common error type identifiers.
    /// </summary>
    public static class ErrorTypes
    {
        public const string ValidationFailed = "validation-failed";
        public const string Unauthorized = "unauthorized";
        public const string Forbidden = "forbidden";
        public const string NotFound = "not-found";
        public const string Conflict = "conflict";
        public const string InternalServerError = "internal-server-error";
        public const string BadRequest = "bad-request";
    }

    /// <summary>
    /// Builds a complete type URI for an error code.
    /// </summary>
    public static string GetTypeUri(string errorType) => $"{BaseTypeUrl}/{errorType}";
}
