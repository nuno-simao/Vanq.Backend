using Microsoft.AspNetCore.Mvc;

namespace Vanq.API.ProblemDetails;

/// <summary>
/// Extended ProblemDetails with Vanq-specific extensions (traceId, timestamp, errorCode).
/// </summary>
public class VanqProblemDetails : Microsoft.AspNetCore.Mvc.ProblemDetails
{
    /// <summary>
    /// Correlation identifier for tracing the request.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// UTC timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Internal error code for client handling (e.g., INVALID_CREDENTIALS).
    /// </summary>
    public string? ErrorCode { get; set; }

    public VanqProblemDetails()
    {
        Timestamp = DateTime.UtcNow;
    }
}
