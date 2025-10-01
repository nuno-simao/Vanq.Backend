using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vanq.API.ProblemDetails;

/// <summary>
/// Fluent builder for creating RFC 7807 compliant Problem Details responses.
/// </summary>
public class ProblemDetailsBuilder
{
    private readonly VanqProblemDetails _problemDetails = new();

    public ProblemDetailsBuilder WithType(string errorType)
    {
        _problemDetails.Type = ProblemDetailsConstants.GetTypeUri(errorType);
        return this;
    }

    public ProblemDetailsBuilder WithTitle(string title)
    {
        _problemDetails.Title = title;
        return this;
    }

    public ProblemDetailsBuilder WithStatus(int status)
    {
        _problemDetails.Status = status;
        return this;
    }

    public ProblemDetailsBuilder WithDetail(string detail)
    {
        _problemDetails.Detail = detail;
        return this;
    }

    public ProblemDetailsBuilder WithInstance(string? instance)
    {
        _problemDetails.Instance = instance;
        return this;
    }

    public ProblemDetailsBuilder WithTraceId(string? traceId)
    {
        _problemDetails.TraceId = traceId;
        return this;
    }

    public ProblemDetailsBuilder WithErrorCode(string? errorCode)
    {
        _problemDetails.ErrorCode = errorCode;
        return this;
    }

    public ProblemDetailsBuilder WithExtension(string key, object? value)
    {
        if (value != null)
        {
            _problemDetails.Extensions[key] = value;
        }
        return this;
    }

    public VanqProblemDetails Build() => _problemDetails;

    /// <summary>
    /// Creates a validation problem details with errors.
    /// </summary>
    public static ValidationProblemDetails CreateValidationProblem(
        IDictionary<string, string[]> errors,
        string? instance = null,
        string? traceId = null)
    {
        var validation = new ValidationProblemDetails(errors)
        {
            Type = ProblemDetailsConstants.GetTypeUri(ProblemDetailsConstants.ErrorTypes.ValidationFailed),
            Title = "One or more validation errors occurred",
            Status = StatusCodes.Status400BadRequest,
            Detail = "The request contains invalid data",
            Instance = instance
        };

        if (!string.IsNullOrEmpty(traceId))
        {
            validation.Extensions[ProblemDetailsConstants.Extensions.TraceId] = traceId;
        }

        validation.Extensions[ProblemDetailsConstants.Extensions.Timestamp] = DateTime.UtcNow;

        return validation;
    }

    /// <summary>
    /// Creates a standard problem details for common scenarios.
    /// </summary>
    public static VanqProblemDetails CreateStandard(
        string errorType,
        string title,
        int status,
        string detail,
        string? instance = null,
        string? traceId = null,
        string? errorCode = null)
    {
        return new ProblemDetailsBuilder()
            .WithType(errorType)
            .WithTitle(title)
            .WithStatus(status)
            .WithDetail(detail)
            .WithInstance(instance)
            .WithTraceId(traceId)
            .WithErrorCode(errorCode)
            .Build();
    }
}
