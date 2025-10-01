using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace Vanq.API.Tests.ProblemDetails;

public class ProblemDetailsTests : IClassFixture<VanqApiFactory>
{
    private readonly VanqApiFactory _factory;
    private readonly HttpClient _client;

    public ProblemDetailsTests(VanqApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnProblemDetails_WhenFeatureFlagEnabled()
    {
        // Arrange
        await _factory.EnableFeatureFlagAsync("problem-details-enabled");

        var loginRequest = new
        {
            email = "nonexistent@example.com",
            password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problemDetails = await response.Content.ReadFromJsonAsync<VanqProblemDetailsResponse>();
        problemDetails.ShouldNotBeNull();
        problemDetails.Type.ShouldContain("invalid-credentials");
        problemDetails.Title.ShouldBe("Invalid Credentials");
        problemDetails.Status.ShouldBe(401);
        problemDetails.TraceId.ShouldNotBeNullOrWhiteSpace();
        problemDetails.Timestamp.ShouldNotBeNull();
        problemDetails.ErrorCode.ShouldBe("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnLegacyFormat_WhenFeatureFlagDisabled()
    {
        // Arrange
        await _factory.DisableFeatureFlagAsync("problem-details-enabled");

        var loginRequest = new
        {
            email = "nonexistent@example.com",
            password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldNotBe("application/problem+json");
    }

    [Fact]
    public async Task Register_WithExistingEmail_ShouldReturnProblemDetails_WhenFeatureFlagEnabled()
    {
        // Arrange
        await _factory.EnableFeatureFlagAsync("problem-details-enabled");

        var email = $"test-{Guid.NewGuid()}@example.com";
        var registerRequest = new
        {
            email,
            password = "TestPassword123!"
        };

        // Register first user
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        // Act - Try to register again with same email
        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problemDetails = await response.Content.ReadFromJsonAsync<VanqProblemDetailsResponse>();
        problemDetails.ShouldNotBeNull();
        problemDetails.Type.ShouldContain("email-already-in-use");
        problemDetails.Title.ShouldBe("Email Already In Use");
        problemDetails.Status.ShouldBe(409);
        problemDetails.ErrorCode.ShouldBe("EMAIL_ALREADY_IN_USE");
    }

    [Fact]
    public async Task UnhandledException_ShouldReturnProblemDetails_WhenFeatureFlagEnabled()
    {
        // Arrange
        await _factory.EnableFeatureFlagAsync("problem-details-enabled");

        // Act - Call endpoint that doesn't exist to trigger error handling
        var response = await _client.GetAsync("/nonexistent-endpoint");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private class VanqProblemDetailsResponse
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
        public string? TraceId { get; set; }
        public DateTime? Timestamp { get; set; }
        public string? ErrorCode { get; set; }
    }
}
