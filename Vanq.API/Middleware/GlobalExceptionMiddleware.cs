using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Vanq.API.ProblemDetails;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Domain.Exceptions;
using Vanq.Shared.Security;

namespace Vanq.API.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions,
/// converts them to standardized HTTP responses (Problem Details when enabled),
/// and logs structured error information.
/// Implements SPEC-0005 (Error Handling Middleware).
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, IFeatureFlagService featureFlagService)
    {
        // Check if error handling middleware is enabled via feature flag
        var isEnabled = await featureFlagService.GetFlagOrDefaultAsync("error-middleware-enabled", defaultValue: true);

        if (!isEnabled)
        {
            // Pass through without handling - allows rollback in case of incidents
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, featureFlagService);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        IFeatureFlagService featureFlagService)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var userId = context.User.TryGetUserId(out var uid) ? uid.ToString() : null;
        var path = context.Request.Path;

        // Determine status code and error details based on exception type
        var (statusCode, errorCode, logLevel) = GetExceptionMetadata(exception);

        // Log the exception with structured data
        LogException(exception, traceId, userId, path, errorCode, statusCode, logLevel);

        // Check if Problem Details is enabled
        var useProblemDetails = await featureFlagService.IsEnabledAsync("problem-details-enabled");

        // Prepare response
        context.Response.ContentType = useProblemDetails ? "application/problem+json" : "application/json";
        context.Response.StatusCode = statusCode;

        object response;

        if (useProblemDetails)
        {
            response = CreateProblemDetailsResponse(exception, context, traceId, errorCode, statusCode);
        }
        else
        {
            response = CreateSimpleErrorResponse(exception, traceId, errorCode, statusCode);
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment(),
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }

    private (int statusCode, string errorCode, LogLevel logLevel) GetExceptionMetadata(Exception exception)
    {
        return exception switch
        {
            DomainException de => (de.HttpStatusCode, de.ErrorCode, GetLogLevelForStatus(de.HttpStatusCode)),
            ArgumentException => (400, "INVALID_ARGUMENT", LogLevel.Warning),
            InvalidOperationException => (400, "INVALID_OPERATION", LogLevel.Warning),
            UnauthorizedAccessException => (403, "FORBIDDEN", LogLevel.Warning),
            KeyNotFoundException => (404, "NOT_FOUND", LogLevel.Warning),
            _ => (500, "INTERNAL_SERVER_ERROR", LogLevel.Error)
        };
    }

    private static LogLevel GetLogLevelForStatus(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 and < 500 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }

    private void LogException(
        Exception exception,
        string traceId,
        string? userId,
        string path,
        string errorCode,
        int statusCode,
        LogLevel logLevel)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["UserId"] = userId,
            ["Path"] = path,
            ["ErrorCode"] = errorCode,
            ["StatusCode"] = statusCode
        });

        _logger.Log(
            logLevel,
            exception,
            "Unhandled exception: {ErrorCode} - {Message}",
            errorCode,
            exception.Message);
    }

    private object CreateProblemDetailsResponse(
        Exception exception,
        HttpContext context,
        string traceId,
        string errorCode,
        int statusCode)
    {
        // Handle validation exceptions specially
        if (exception is ValidationException validationEx && validationEx.Errors.Count > 0)
        {
            return ProblemDetailsBuilder.CreateValidationProblem(
                validationEx.Errors,
                context.Request.Path,
                traceId);
        }

        // Standard problem details
        var title = GetErrorTitle(statusCode);
        var detail = GetErrorDetail(exception, statusCode);
        var type = GetErrorType(errorCode);

        var problemDetails = ProblemDetailsBuilder.CreateStandard(
            type,
            title,
            statusCode,
            detail,
            context.Request.Path,
            traceId,
            errorCode);

        // In development, add additional debug information
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
        }

        return problemDetails;
    }

    private object CreateSimpleErrorResponse(
        Exception exception,
        string traceId,
        string errorCode,
        int statusCode)
    {
        return new
        {
            error = errorCode,
            message = GetErrorDetail(exception, statusCode),
            traceId,
            timestamp = DateTime.UtcNow
        };
    }

    private string GetErrorTitle(int statusCode)
    {
        return statusCode switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            500 => "Internal Server Error",
            _ => "Error"
        };
    }

    private string GetErrorDetail(Exception exception, int statusCode)
    {
        // In production, mask internal error details for 500 errors
        if (statusCode >= 500 && !_environment.IsDevelopment())
        {
            return "An unexpected error occurred. Please contact support with the trace ID.";
        }

        // For client errors (4xx), return the exception message
        return exception.Message;
    }

    private static string GetErrorType(string errorCode)
    {
        return errorCode.ToLowerInvariant().Replace('_', '-');
    }
}
