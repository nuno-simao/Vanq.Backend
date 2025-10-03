using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Vanq.API.Middleware;
using Vanq.API.ProblemDetails;
using Vanq.Application.Abstractions.FeatureFlags;
using Vanq.Domain.Exceptions;
using Xunit;

namespace Vanq.API.Tests.Middleware;

public class GlobalExceptionMiddlewareTests
{
    private readonly GlobalExceptionMiddleware _middleware;
    private readonly FakeFeatureFlagService _featureFlagService;
    private readonly FakeHostEnvironment _hostEnvironment;

    public GlobalExceptionMiddlewareTests()
    {
        _featureFlagService = new FakeFeatureFlagService();
        _hostEnvironment = new FakeHostEnvironment { EnvironmentName = "Development" };
        _middleware = new GlobalExceptionMiddleware(
            next: _ => throw new InvalidOperationException("Test exception"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnProblemDetails_WhenProblemDetailsEnabled()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new InvalidOperationException("Test error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(400);
        context.Response.ContentType.ShouldBe("application/problem+json");

        var responseBody = await ReadResponseBodyAsync(context);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody, options);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Title.ShouldBe("Bad Request");
        problemDetails.ErrorCode.ShouldBe("INVALID_OPERATION");
        problemDetails.TraceId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnSimpleJson_WhenProblemDetailsDisabled()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", false);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new InvalidOperationException("Test error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(400);
        context.Response.ContentType.ShouldBe("application/json");

        var responseBody = await ReadResponseBodyAsync(context);
        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

        response.GetProperty("error").GetString().ShouldBe("INVALID_OPERATION");
        response.GetProperty("message").GetString().ShouldBe("Test error");
        response.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn400_WhenValidationExceptionThrown()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var errors = new Dictionary<string, string[]>
        {
            ["email"] = new[] { "Email is required" },
            ["password"] = new[] { "Password must be at least 8 characters" }
        };

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new ValidationException("Validation failed", errors),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn404_WhenNotFoundExceptionThrown()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new NotFoundException("User", Guid.NewGuid()),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(404);

        var responseBody = await ReadResponseBodyAsync(context);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody, options);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(404);
        problemDetails.ErrorCode.ShouldBe("NOT_FOUND");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn401_WhenUnauthorizedExceptionThrown()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new UnauthorizedException(),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(401);

        var responseBody = await ReadResponseBodyAsync(context);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody, options);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(401);
        problemDetails.ErrorCode.ShouldBe("UNAUTHORIZED");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn403_WhenForbiddenExceptionThrown()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new ForbiddenException(),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(403);

        var responseBody = await ReadResponseBodyAsync(context);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody, options);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(403);
        problemDetails.ErrorCode.ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn409_WhenConflictExceptionThrown()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new ConflictException("Email already exists"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(409);

        var responseBody = await ReadResponseBodyAsync(context);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody, options);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(409);
        problemDetails.ErrorCode.ShouldBe("CONFLICT");
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturn500_WhenUnknownExceptionThrown()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new Exception("Unexpected error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(500);

        var responseBody = await ReadResponseBodyAsync(context);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody, options);

        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(500);
        problemDetails.ErrorCode.ShouldBe("INTERNAL_SERVER_ERROR");
    }

    [Fact]
    public async Task InvokeAsync_ShouldMaskErrorDetails_WhenProductionEnvironment()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var productionEnvironment = new FakeHostEnvironment { EnvironmentName = "Production" };
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new Exception("Internal database error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: productionEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        context.Response.StatusCode.ShouldBe(500);

        var responseBody = await ReadResponseBodyAsync(context);
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody);

        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("An unexpected error occurred. Please contact support with the trace ID.");
    }

    [Fact]
    public async Task InvokeAsync_ShouldIncludeExceptionType_WhenDevelopmentEnvironment()
    {
        // Arrange
        _featureFlagService.SetFlag("problem-details-enabled", true);
        var context = CreateHttpContext();

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new InvalidOperationException("Test error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            environment: _hostEnvironment);

        // Act
        await middleware.InvokeAsync(context, _featureFlagService);

        // Assert
        var responseBody = await ReadResponseBodyAsync(context);
        var problemDetails = JsonSerializer.Deserialize<VanqProblemDetails>(responseBody);

        problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("exceptionType");
        problemDetails.Extensions["exceptionType"].ToString().ShouldBe("InvalidOperationException");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/test";
        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private class FakeFeatureFlagService : IFeatureFlagService
    {
        private readonly Dictionary<string, bool> _flags = new();

        public void SetFlag(string name, bool value) => _flags[name] = value;

        public Task<bool> IsEnabledAsync(string flagName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_flags.TryGetValue(flagName, out var value) && value);
        }

        public Task<bool> GetFlagOrDefaultAsync(string flagName, bool defaultValue = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_flags.TryGetValue(flagName, out var value) ? value : defaultValue);
        }

        public Task<Vanq.Application.Contracts.FeatureFlags.FeatureFlagDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<List<Vanq.Application.Contracts.FeatureFlags.FeatureFlagDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<List<Vanq.Application.Contracts.FeatureFlags.FeatureFlagDto>> GetByEnvironmentAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Vanq.Application.Contracts.FeatureFlags.FeatureFlagDto> CreateAsync(Vanq.Application.Contracts.FeatureFlags.CreateFeatureFlagDto request, string? updatedBy = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Vanq.Application.Contracts.FeatureFlags.FeatureFlagDto?> UpdateAsync(string key, Vanq.Application.Contracts.FeatureFlags.UpdateFeatureFlagDto request, string? updatedBy = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Vanq.Application.Contracts.FeatureFlags.FeatureFlagDto?> ToggleAsync(string key, string? updatedBy = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
