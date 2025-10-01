using Microsoft.Extensions.Logging;
using Shouldly;
using Vanq.Infrastructure.Logging.Extensions;
using Xunit;

namespace Vanq.Infrastructure.Tests.Logging;

public class LoggerExtensionsTests
{
    private readonly TestLogger _logger;

    public LoggerExtensionsTests()
    {
        _logger = new TestLogger();
    }

    [Fact]
    public void LogAuthEvent_ShouldLogWithSuccessLevel_WhenStatusIsSuccess()
    {
        // Act
        _logger.LogAuthEvent("UserLogin", "success", userId: Guid.NewGuid(), email: "user@example.com");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Information);
        _logger.LastMessage.ShouldContain("UserLogin");
        _logger.LastMessage.ShouldContain("success");
    }

    [Fact]
    public void LogAuthEvent_ShouldLogWithWarningLevel_WhenStatusIsFailure()
    {
        // Act
        _logger.LogAuthEvent("UserLogin", "failure", email: "user@example.com", reason: "InvalidCredentials");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Warning);
        _logger.LastMessage.ShouldContain("UserLogin");
        _logger.LastMessage.ShouldContain("failure");
    }

    [Fact]
    public void LogAuthEvent_ShouldIncludeAllParameters_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        _logger.LogAuthEvent("UserLogin", "success", userId: userId, email: "user@example.com", reason: "ValidCredentials");

        // Assert
        _logger.LastMessage.ShouldContain("UserLogin");
        _logger.LastMessage.ShouldContain("success");
        _logger.LastState.ShouldContain(kvp => kvp.Key == "UserId" && kvp.Value != null && kvp.Value.ToString() == userId.ToString());
    }

    [Fact]
    public void LogRbacEvent_ShouldLogWithSuccessLevel_WhenStatusIsSuccess()
    {
        // Act
        _logger.LogRbacEvent("RoleAssigned", "success", targetUserId: Guid.NewGuid(), roleName: "admin");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Information);
        _logger.LastMessage.ShouldContain("RoleAssigned");
        _logger.LastMessage.ShouldContain("success");
    }

    [Fact]
    public void LogRbacEvent_ShouldIncludeRoleAndPermission_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        _logger.LogRbacEvent(
            "PermissionGranted",
            "success",
            targetUserId: userId,
            roleName: "admin",
            permissionName: "rbac:role:create"
        );

        // Assert
        _logger.LastMessage.ShouldContain("PermissionGranted");
        _logger.LastState.ShouldContain(kvp => kvp.Key == "RoleName" && kvp.Value != null && kvp.Value.ToString() == "admin");
        _logger.LastState.ShouldContain(kvp => kvp.Key == "PermissionName" && kvp.Value != null && kvp.Value.ToString() == "rbac:role:create");
    }

    [Fact]
    public void LogFeatureFlagEvent_ShouldLogAtDebugLevel_Always()
    {
        // Act
        _logger.LogFeatureFlagEvent("rbac-enabled", true, "Development", "cache hit");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Debug);
        _logger.LastMessage.ShouldContain("rbac-enabled");
        _logger.LastMessage.ShouldContain("True");
        _logger.LastMessage.ShouldContain("Development");
    }

    [Fact]
    public void LogSecurityEvent_ShouldLogAtCriticalLevel_WhenSeverityIsCritical()
    {
        // Act
        _logger.LogSecurityEvent("SecurityBreach", "critical", ipAddress: "192.168.1.100", details: "Unauthorized access attempt");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Critical);
        _logger.LastMessage.ShouldContain("SecurityBreach");
        _logger.LastMessage.ShouldContain("critical");
    }

    [Fact]
    public void LogSecurityEvent_ShouldLogAtErrorLevel_WhenSeverityIsHigh()
    {
        // Act
        _logger.LogSecurityEvent("SuspiciousActivity", "high", ipAddress: "192.168.1.100");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public void LogSecurityEvent_ShouldLogAtWarningLevel_WhenSeverityIsMedium()
    {
        // Act
        _logger.LogSecurityEvent("RateLimitExceeded", "medium", ipAddress: "192.168.1.100");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public void LogSecurityEvent_ShouldLogAtInformationLevel_WhenSeverityIsLow()
    {
        // Act
        _logger.LogSecurityEvent("LoginAttempt", "low", ipAddress: "192.168.1.100");

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public void LogPerformanceEvent_ShouldLogAtInformationLevel_WhenBelowThreshold()
    {
        // Act
        _logger.LogPerformanceEvent("DatabaseQuery", elapsedMilliseconds: 500, threshold: 1000);

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Information);
        _logger.LastMessage.ShouldContain("500");
        _logger.LastMessage.ShouldContain("False"); // Not slow
    }

    [Fact]
    public void LogPerformanceEvent_ShouldLogAtWarningLevel_WhenAboveThreshold()
    {
        // Act
        _logger.LogPerformanceEvent("DatabaseQuery", elapsedMilliseconds: 1500, threshold: 1000);

        // Assert
        _logger.LastLogLevel.ShouldBe(LogLevel.Warning);
        _logger.LastMessage.ShouldContain("1500");
        _logger.LastMessage.ShouldContain("True"); // Is slow
    }

    [Fact]
    public void LogDomainEvent_ShouldIncludeAllProperties_WhenProvided()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["OrderId"] = Guid.NewGuid(),
            ["Amount"] = 100.50m,
            ["ItemCount"] = 3
        };

        // Act
        _logger.LogDomainEvent("OrderCreated", "success", properties);

        // Assert
        _logger.LastMessage.ShouldContain("OrderCreated");
        _logger.LastMessage.ShouldContain("success");
        _logger.LastState.ShouldContain(kvp => kvp.Key == "OrderId");
        _logger.LastState.ShouldContain(kvp => kvp.Key == "Amount");
        _logger.LastState.ShouldContain(kvp => kvp.Key == "ItemCount");
    }
}

// Test helper class to capture log calls
internal class TestLogger : ILogger
{
    public LogLevel LastLogLevel { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public List<KeyValuePair<string, object?>> LastState { get; private set; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        LastLogLevel = logLevel;
        LastMessage = formatter(state, exception);

        if (state is IEnumerable<KeyValuePair<string, object?>> stateDict)
        {
            LastState = stateDict.ToList();
        }
    }
}
