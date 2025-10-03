using System.Diagnostics;

namespace Vanq.API.Middleware;

/// <summary>
/// Middleware to log CORS requests and blocked origins (NFR-02)
/// Implements structured logging for observability
/// </summary>
public sealed class CorsLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorsLoggingMiddleware> _logger;

    public CorsLoggingMiddleware(RequestDelegate next, ILogger<CorsLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var hasCorsRequest = !string.IsNullOrEmpty(origin);

        if (hasCorsRequest)
        {
            var stopwatch = Stopwatch.StartNew();

            // Execute next middleware
            await _next(context);

            stopwatch.Stop();

            // Check if CORS headers were added (indicates allowed origin)
            var hasAllowOriginHeader = context.Response.Headers.ContainsKey("Access-Control-Allow-Origin");

            if (!hasAllowOriginHeader)
            {
                // NFR-02: Log blocked CORS request
                _logger.LogWarning(
                    "CORS request blocked. Event={Event}, Origin={Origin}, Path={Path}, Method={Method}, StatusCode={StatusCode}, Duration={Duration}ms",
                    "cors-blocked",
                    origin,
                    context.Request.Path,
                    context.Request.Method,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds
                );
            }
            else
            {
                // Log successful CORS request (debug level)
                _logger.LogDebug(
                    "CORS request allowed. Event={Event}, Origin={Origin}, Path={Path}, Method={Method}, Duration={Duration}ms",
                    "cors-allowed",
                    origin,
                    context.Request.Path,
                    context.Request.Method,
                    stopwatch.ElapsedMilliseconds
                );
            }

            // NFR-03: Performance tracking
            if (context.Request.Method == "OPTIONS" && stopwatch.ElapsedMilliseconds > 120)
            {
                _logger.LogWarning(
                    "CORS preflight slow response. Event={Event}, Origin={Origin}, Path={Path}, Duration={Duration}ms, Threshold=120ms",
                    "cors-preflight-slow",
                    origin,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds
                );
            }
        }
        else
        {
            // No CORS request, continue normally
            await _next(context);
        }
    }
}

/// <summary>
/// Extension method to register CORS logging middleware
/// </summary>
public static class CorsLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseCorsLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorsLoggingMiddleware>();
    }
}
