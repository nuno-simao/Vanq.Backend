using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Vanq.Infrastructure.Logging.Middleware;

public sealed class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly LoggingOptions _options;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IOptions<LoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableRequestLogging)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            stopwatch.Stop();

            await LogRequest(context, stopwatch.ElapsedMilliseconds);

            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private Task LogRequest(HttpContext context, long elapsedMs)
    {
        var request = context.Request;
        var response = context.Response;

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error :
                       response.StatusCode >= 400 ? LogLevel.Warning :
                       LogLevel.Information;

        _logger.Log(
            logLevel,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | TraceId: {TraceId} | UserId: {UserId}",
            request.Method,
            request.Path,
            response.StatusCode,
            elapsedMs,
            context.TraceIdentifier,
            context.User?.FindFirst("sub")?.Value ?? "anonymous"
        );

        return Task.CompletedTask;
    }
}
