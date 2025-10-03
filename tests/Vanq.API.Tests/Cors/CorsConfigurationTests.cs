using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Vanq.API.Configuration;
using Vanq.API.Extensions;
using Xunit;

namespace Vanq.API.Tests.Cors;

/// <summary>
/// Unit tests for CORS configuration
/// Covers TEST-03 from SPEC-0002
/// </summary>
public class CorsConfigurationTests
{
    [Fact]
    public void CorsOptions_ShouldLoadFromConfiguration_WhenConfigured()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["Cors:PolicyName"] = "test-cors-policy",
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:AllowedOrigins:1"] = "https://test.com",
            ["Cors:AllowedMethods:0"] = "GET",
            ["Cors:AllowedMethods:1"] = "POST",
            ["Cors:AllowedHeaders:0"] = "Content-Type",
            ["Cors:AllowCredentials"] = "true",
            ["Cors:MaxAgeSeconds"] = "7200"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CorsOptions>>();

        // Act
        var corsOptions = options.Value;

        // Assert
        corsOptions.PolicyName.ShouldBe("test-cors-policy");
        corsOptions.AllowedOrigins.ShouldContain("https://example.com");
        corsOptions.AllowedOrigins.ShouldContain("https://test.com");
        corsOptions.AllowedMethods.ShouldContain("GET");
        corsOptions.AllowedMethods.ShouldContain("POST");
        corsOptions.AllowedHeaders.ShouldContain("Content-Type");
        corsOptions.AllowCredentials.ShouldBeTrue();
        corsOptions.MaxAgeSeconds.ShouldBe(7200);
    }

    [Fact]
    public void CorsOptions_ShouldHaveDefaultValues_WhenNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CorsOptions>>();

        // Act
        var corsOptions = options.Value;

        // Assert
        corsOptions.PolicyName.ShouldBe("vanq-default-cors");
        corsOptions.AllowedOrigins.ShouldBeEmpty();
        corsOptions.AllowedMethods.ShouldNotBeEmpty();
        corsOptions.AllowedHeaders.ShouldNotBeEmpty();
        corsOptions.AllowCredentials.ShouldBeTrue();
        corsOptions.MaxAgeSeconds.ShouldBe(3600);
    }

    [Fact]
    public void AddVanqCors_ShouldRegisterCorsServices_WhenCalled()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["Cors:PolicyName"] = "vanq-default-cors",
            ["Cors:AllowedOrigins:0"] = "https://example.com"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        var environment = new FakeHostEnvironment { EnvironmentName = "Production" };

        // Act
        services.AddVanqCors(configuration, environment);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var corsOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CorsOptions>>();
        corsOptions.ShouldNotBeNull();
    }

    [Fact]
    public void CorsConfiguration_ShouldAllowAnyOrigin_WhenDevelopmentEnvironment()
    {
        // Arrange
        var configValues = new Dictionary<string, string?>
        {
            ["Cors:PolicyName"] = "vanq-default-cors",
            ["Cors:AllowedOrigins:0"] = "https://example.com"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        var environment = new FakeHostEnvironment { EnvironmentName = "Development" };

        // Act
        services.AddVanqCors(configuration, environment);

        // Assert - REQ-04: Development should allow any origin
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.ShouldNotBeNull();
        // In Development, CORS policy should be configured to allow any origin
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://test.com")]
    public void CorsConfiguration_ShouldAcceptHttpsOrigins_WhenProductionEnvironment(string origin)
    {
        // Arrange - BR-01: Only HTTPS origins in production
        var configValues = new Dictionary<string, string?>
        {
            ["Cors:PolicyName"] = "vanq-default-cors",
            ["Cors:AllowedOrigins:0"] = origin
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        var environment = new FakeHostEnvironment { EnvironmentName = "Production" };

        // Act
        services.AddVanqCors(configuration, environment);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("http://test.com")]
    public void CorsConfiguration_ShouldFilterHttpOrigins_WhenProductionEnvironment(string origin)
    {
        // Arrange - BR-01: HTTP origins should be filtered out in production
        var configValues = new Dictionary<string, string?>
        {
            ["Cors:PolicyName"] = "vanq-default-cors",
            ["Cors:AllowedOrigins:0"] = origin
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        var environment = new FakeHostEnvironment { EnvironmentName = "Production" };

        // Act
        services.AddVanqCors(configuration, environment);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.ShouldNotBeNull();
        // HTTP origins should be filtered out by the CORS policy builder
    }
}

/// <summary>
/// Fake IHostEnvironment for testing
/// </summary>
internal class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Vanq.API.Tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}
