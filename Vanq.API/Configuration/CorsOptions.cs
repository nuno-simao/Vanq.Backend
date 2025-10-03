namespace Vanq.API.Configuration;

/// <summary>
/// CORS configuration options loaded from appsettings.json
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Name of the CORS policy to register
    /// </summary>
    public string PolicyName { get; init; } = "vanq-default-cors";

    /// <summary>
    /// List of allowed origins (URLs). Empty for Development environment (allows any origin).
    /// Must be HTTPS in Production per BR-01.
    /// </summary>
    public List<string> AllowedOrigins { get; init; } = [];

    /// <summary>
    /// HTTP methods allowed for CORS requests
    /// </summary>
    public List<string> AllowedMethods { get; init; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"];

    /// <summary>
    /// Headers allowed in CORS requests
    /// </summary>
    public List<string> AllowedHeaders { get; init; } = ["Content-Type", "Authorization", "Accept", "Origin", "X-Requested-With"];

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers)
    /// Per BR-03: When true, cannot use AllowAnyOrigin
    /// </summary>
    public bool AllowCredentials { get; init; } = true;

    /// <summary>
    /// Max age in seconds for preflight cache (Access-Control-Max-Age)
    /// </summary>
    public int MaxAgeSeconds { get; init; } = 3600;
}
