using Microsoft.Extensions.Logging;

namespace Vanq.Infrastructure.Logging.Extensions;

public static class LoggerExtensions
{
    /// <summary>
    /// Logs an authentication event with structured format
    /// </summary>
    public static void LogAuthEvent(
        this ILogger logger,
        string eventName,
        string status,
        Guid? userId = null,
        string? email = null,
        string? reason = null)
    {
        var logLevel = status == "success" ? LogLevel.Information : LogLevel.Warning;

        logger.Log(
            logLevel,
            "Auth Event: {Event} | Status: {Status} | UserId: {UserId} | Email: {Email} | Reason: {Reason}",
            eventName,
            status,
            userId?.ToString() ?? "N/A",
            email ?? "N/A",
            reason ?? "N/A"
        );
    }

    /// <summary>
    /// Logs a domain event with structured format
    /// </summary>
    public static void LogDomainEvent(
        this ILogger logger,
        string eventName,
        string status,
        Dictionary<string, object>? properties = null)
    {
        var logLevel = status == "success" ? LogLevel.Information : LogLevel.Warning;

        var message = $"Domain Event: {{Event}} | Status: {{Status}}";
        var args = new List<object> { eventName, status };

        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                message += $" | {kvp.Key}: {{{kvp.Key}}}";
                args.Add(kvp.Value);
            }
        }

        logger.Log(logLevel, message, args.ToArray());
    }

    /// <summary>
    /// Logs a feature flag evaluation event
    /// </summary>
    public static void LogFeatureFlagEvent(
        this ILogger logger,
        string flagName,
        bool isEnabled,
        string environment)
    {
        logger.LogInformation(
            "Feature Flag Evaluated: {FlagName} | Enabled: {IsEnabled} | Environment: {Environment}",
            flagName,
            isEnabled,
            environment
        );
    }

    /// <summary>
    /// Logs an RBAC event (role/permission changes)
    /// </summary>
    public static void LogRbacEvent(
        this ILogger logger,
        string eventName,
        string status,
        Guid? targetUserId = null,
        string? roleName = null,
        string? permissionName = null,
        Guid? actorId = null)
    {
        var logLevel = status == "success" ? LogLevel.Information : LogLevel.Warning;

        logger.Log(
            logLevel,
            "RBAC Event: {Event} | Status: {Status} | TargetUser: {TargetUserId} | Role: {RoleName} | Permission: {PermissionName} | Actor: {ActorId}",
            eventName,
            status,
            targetUserId?.ToString() ?? "N/A",
            roleName ?? "N/A",
            permissionName ?? "N/A",
            actorId?.ToString() ?? "N/A"
        );
    }

    /// <summary>
    /// Logs a security event (suspicious activity, validation failures, etc.)
    /// </summary>
    public static void LogSecurityEvent(
        this ILogger logger,
        string eventName,
        string severity,
        string? ipAddress = null,
        Guid? userId = null,
        string? details = null)
    {
        var logLevel = severity switch
        {
            "critical" => LogLevel.Critical,
            "high" => LogLevel.Error,
            "medium" => LogLevel.Warning,
            _ => LogLevel.Information
        };

        logger.Log(
            logLevel,
            "Security Event: {Event} | Severity: {Severity} | IP: {IpAddress} | UserId: {UserId} | Details: {Details}",
            eventName,
            severity,
            ipAddress ?? "N/A",
            userId?.ToString() ?? "N/A",
            details ?? "N/A"
        );
    }

    /// <summary>
    /// Logs a performance event (slow queries, timeouts, etc.)
    /// </summary>
    public static void LogPerformanceEvent(
        this ILogger logger,
        string operationName,
        long elapsedMilliseconds,
        long? threshold = null)
    {
        var isSlow = threshold.HasValue && elapsedMilliseconds > threshold.Value;
        var logLevel = isSlow ? LogLevel.Warning : LogLevel.Information;

        logger.Log(
            logLevel,
            "Performance: {Operation} | Duration: {ElapsedMs}ms | Slow: {IsSlow}",
            operationName,
            elapsedMilliseconds,
            isSlow
        );
    }
}
