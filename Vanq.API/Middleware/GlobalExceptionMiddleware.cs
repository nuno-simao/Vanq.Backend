using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vanq.API.ProblemDetails;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Infrastructure.Logging.Extensions;

namespace Vanq.API.Middleware;

/// <summary>
/// Global exception handling middleware that converts unhandled exceptions to RFC 7807 Problem Details.
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

        // Log the exception with structured logging
        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}, Method: {Method}",
            traceId,
            context.Request.Path.ToString(),
            context.Request.Method
        );

        // Check if Problem Details is enabled
        var isProblemDetailsEnabled = await featureFlagService.IsEnabledAsync("problem-details-enabled");

        if (!isProblemDetailsEnabled)
        {
            // Fallback to simple JSON error response
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var fallbackResponse = new
            {
                error = "An error occurred while processing your request",
                traceId
            };

            await context.Response.WriteAsJsonAsync(fallbackResponse);
            return;
        }

        // Build Problem Details response
        var problemDetails = CreateProblemDetails(exception, context, traceId);

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsJsonAsync(problemDetails, options);
    }

    private VanqProblemDetails CreateProblemDetails(
        Exception exception,
        HttpContext context,
        string traceId)
    {
        var (status, errorType, title) = MapExceptionToStatus(exception);

        var detail = _environment.IsDevelopment()
            ? exception.Message
            : "An error occurred while processing your request";

        var problemDetails = ProblemDetailsBuilder.CreateStandard(
            errorType: errorType,
            title: title,
            status: status,
            detail: detail,
            instance: context.Request.Path,
            traceId: traceId
        );

        // In development, add additional debug information (but never stack trace in production)
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
        }

        return problemDetails;
    }

    private static (int status, string errorType, string title) MapExceptionToStatus(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException => (
                StatusCodes.Status400BadRequest,
                ProblemDetailsConstants.ErrorTypes.BadRequest,
                "Bad Request"
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ProblemDetailsConstants.ErrorTypes.Unauthorized,
                "Unauthorized"
            ),
            InvalidOperationException => (
                StatusCodes.Status409Conflict,
                ProblemDetailsConstants.ErrorTypes.Conflict,
                "Conflict"
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                ProblemDetailsConstants.ErrorTypes.NotFound,
                "Not Found"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                ProblemDetailsConstants.ErrorTypes.InternalServerError,
                "Internal Server Error"
            )
        };
    }
}
