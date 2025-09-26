# Fase 4 - Parte 9: Monitoring & Observability

## Contexto da Implementação

Esta é a **nona parte da Fase 4** focada na **implementação completa de monitoramento** e **observabilidade** com métricas, logs, alertas e dashboards para acompanhamento de performance e saúde da aplicação.

### Objetivos da Parte 9
✅ **Application Insights** integration completa  
✅ **Structured logging** com Serilog  
✅ **Custom metrics** e dashboards  
✅ **Health monitoring** avançado  
✅ **SLA monitoring** e alertas  
✅ **Performance tracking** detalhado  

### Pré-requisitos
- Partes 1-8 implementadas e deployadas
- Azure Application Insights configurado
- Prometheus/Grafana (opcional)
- Log aggregation system (Azure Monitor/ELK)

---

## 8. Monitoring & Observability

### 8.1 Application Insights Integration

Integração completa com Azure Application Insights para telemetria, métricas e rastreamento de performance.

#### IDE.Infrastructure/Telemetry/TelemetryConfiguration.cs
```csharp
public static class TelemetryConfiguration
{
    public static IServiceCollection AddApplicationInsights(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("ApplicationInsights:ConnectionString");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Application Insights connection string is required");
        }

        // Add Application Insights telemetry
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = connectionString;
            options.EnableQuickPulseMetricStream = true;
            options.EnableAdaptiveSampling = true;
            options.EnableHeartbeat = true;
            options.AddAutoCollectedMetricExtractor = true;
            options.EnableActiveTelemetryConfigurationSetup = true;
        });

        // Custom telemetry initializers
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
        services.AddSingleton<ITelemetryInitializer, UserTelemetryInitializer>();
        services.AddSingleton<ITelemetryInitializer, WorkspaceTelemetryInitializer>();

        // Custom telemetry processors
        services.AddSingleton<ITelemetryProcessor, SensitiveDataTelemetryProcessor>();
        services.AddSingleton<ITelemetryProcessor, PerformanceTelemetryProcessor>();

        // Dependency injection for telemetry client
        services.AddScoped<ICustomTelemetryClient, CustomTelemetryClient>();
        services.AddScoped<IPerformanceTracker, PerformanceTracker>();
        services.AddScoped<ISlaMonitoring, SlaMonitoring>();

        return services;
    }
}

public class CustomTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry.Context.Cloud.RoleName == null)
        {
            telemetry.Context.Cloud.RoleName = "IDE-Backend";
        }

        if (telemetry.Context.Cloud.RoleInstance == null)
        {
            telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
        }

        // Add custom properties
        telemetry.Context.GlobalProperties.TryAdd("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown");
        telemetry.Context.GlobalProperties.TryAdd("Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
        telemetry.Context.GlobalProperties.TryAdd("BuildTime", GetBuildTime());
    }

    private string GetBuildTime()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class UserTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userPlan = httpContext.User.FindFirst("plan")?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                telemetry.Context.User.Id = userId;
                telemetry.Context.GlobalProperties.TryAdd("UserPlan", userPlan ?? "Unknown");
            }
        }
    }
}

public class WorkspaceTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WorkspaceTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var workspaceId = httpContext.Request.Headers["X-Workspace-Id"].FirstOrDefault() ??
                             httpContext.Request.Query["workspaceId"].FirstOrDefault();

            if (!string.IsNullOrEmpty(workspaceId))
            {
                telemetry.Context.GlobalProperties.TryAdd("WorkspaceId", workspaceId);
            }
        }
    }
}
```

### Custom Telemetry Client

#### IDE.Infrastructure/Telemetry/ICustomTelemetryClient.cs
```csharp
public interface ICustomTelemetryClient
{
    void TrackUserAction(string action, Guid userId, Dictionary<string, string> properties = null);
    void TrackWorkspaceOperation(string operation, Guid workspaceId, Guid userId, TimeSpan duration, bool success);
    void TrackCollaborationEvent(string eventType, Guid workspaceId, Guid userId, Dictionary<string, object> metrics = null);
    void TrackPerformanceMetric(string metricName, double value, Dictionary<string, string> properties = null);
    void TrackCustomEvent(string eventName, Dictionary<string, string> properties = null, Dictionary<string, double> metrics = null);
    void TrackException(Exception exception, Dictionary<string, string> properties = null);
    void TrackDependency(string dependencyType, string target, string command, DateTimeOffset startTime, TimeSpan duration, bool success);
}

public class CustomTelemetryClient : ICustomTelemetryClient
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<CustomTelemetryClient> _logger;

    public CustomTelemetryClient(TelemetryClient telemetryClient, ILogger<CustomTelemetryClient> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackUserAction(string action, Guid userId, Dictionary<string, string> properties = null)
    {
        try
        {
            var props = properties ?? new Dictionary<string, string>();
            props["UserId"] = userId.ToString();
            props["Action"] = action;
            props["Timestamp"] = DateTime.UtcNow.ToString("O");

            _telemetryClient.TrackEvent($"UserAction.{action}", props);
            
            _logger.LogInformation("User action tracked: {Action} for user {UserId}", action, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking user action: {Action}", action);
        }
    }

    public void TrackWorkspaceOperation(string operation, Guid workspaceId, Guid userId, TimeSpan duration, bool success)
    {
        try
        {
            var properties = new Dictionary<string, string>
            {
                ["WorkspaceId"] = workspaceId.ToString(),
                ["UserId"] = userId.ToString(),
                ["Operation"] = operation,
                ["Success"] = success.ToString(),
                ["Duration"] = duration.TotalMilliseconds.ToString("F2")
            };

            var metrics = new Dictionary<string, double>
            {
                ["DurationMs"] = duration.TotalMilliseconds,
                ["Success"] = success ? 1 : 0
            };

            _telemetryClient.TrackEvent($"WorkspaceOperation.{operation}", properties, metrics);
            _telemetryClient.TrackMetric($"WorkspaceOperation.{operation}.Duration", duration.TotalMilliseconds);
            
            if (!success)
            {
                _telemetryClient.TrackMetric($"WorkspaceOperation.{operation}.Failures", 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking workspace operation: {Operation}", operation);
        }
    }

    public void TrackCollaborationEvent(string eventType, Guid workspaceId, Guid userId, Dictionary<string, object> metrics = null)
    {
        try
        {
            var properties = new Dictionary<string, string>
            {
                ["EventType"] = eventType,
                ["WorkspaceId"] = workspaceId.ToString(),
                ["UserId"] = userId.ToString(),
                ["Timestamp"] = DateTime.UtcNow.ToString("O")
            };

            var telemetryMetrics = new Dictionary<string, double>();
            if (metrics != null)
            {
                foreach (var metric in metrics)
                {
                    if (double.TryParse(metric.Value.ToString(), out var value))
                    {
                        telemetryMetrics[metric.Key] = value;
                    }
                    else
                    {
                        properties[metric.Key] = metric.Value.ToString();
                    }
                }
            }

            _telemetryClient.TrackEvent($"Collaboration.{eventType}", properties, telemetryMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking collaboration event: {EventType}", eventType);
        }
    }

    public void TrackPerformanceMetric(string metricName, double value, Dictionary<string, string> properties = null)
    {
        try
        {
            _telemetryClient.TrackMetric(metricName, value, properties);
            
            // Also track as custom event for additional context
            var eventProperties = properties ?? new Dictionary<string, string>();
            eventProperties["MetricName"] = metricName;
            eventProperties["Value"] = value.ToString("F2");
            
            _telemetryClient.TrackEvent("PerformanceMetric", eventProperties);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking performance metric: {MetricName}", metricName);
        }
    }

    public void TrackCustomEvent(string eventName, Dictionary<string, string> properties = null, Dictionary<string, double> metrics = null)
    {
        try
        {
            _telemetryClient.TrackEvent(eventName, properties, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking custom event: {EventName}", eventName);
        }
    }

    public void TrackException(Exception exception, Dictionary<string, string> properties = null)
    {
        try
        {
            var props = properties ?? new Dictionary<string, string>();
            props["ExceptionType"] = exception.GetType().Name;
            props["Timestamp"] = DateTime.UtcNow.ToString("O");

            _telemetryClient.TrackException(exception, props);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking exception");
        }
    }

    public void TrackDependency(string dependencyType, string target, string command, DateTimeOffset startTime, TimeSpan duration, bool success)
    {
        try
        {
            _telemetryClient.TrackDependency(dependencyType, target, command, startTime, duration, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking dependency: {DependencyType}", dependencyType);
        }
    }
}
```

### Performance Tracking

#### IDE.Infrastructure/Performance/IPerformanceTracker.cs
```csharp
public interface IPerformanceTracker
{
    IDisposable TrackOperation(string operationName, Dictionary<string, string> properties = null);
    void TrackMetric(string metricName, double value, Dictionary<string, string> properties = null);
    Task<PerformanceReport> GenerateReportAsync(DateTime from, DateTime to);
    void TrackApiEndpoint(string endpoint, string method, int statusCode, TimeSpan duration);
    void TrackDatabaseQuery(string queryType, TimeSpan duration, bool success);
}

public class PerformanceTracker : IPerformanceTracker
{
    private readonly ICustomTelemetryClient _telemetryClient;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<PerformanceTracker> _logger;

    public PerformanceTracker(
        ICustomTelemetryClient telemetryClient, 
        IRedisCacheService cache, 
        ILogger<PerformanceTracker> logger)
    {
        _telemetryClient = telemetryClient;
        _cache = cache;
        _logger = logger;
    }

    public IDisposable TrackOperation(string operationName, Dictionary<string, string> properties = null)
    {
        return new OperationTracker(operationName, properties, this);
    }

    public void TrackMetric(string metricName, double value, Dictionary<string, string> properties = null)
    {
        _telemetryClient.TrackPerformanceMetric(metricName, value, properties);
        
        // Store in Redis for aggregation
        var key = $"metrics:{metricName}:{DateTime.UtcNow:yyyyMMddHH}";
        _ = Task.Run(async () =>
        {
            try
            {
                await _cache.IncrementAsync(key, (long)value, TimeSpan.FromHours(25));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store metric in Redis: {MetricName}", metricName);
            }
        });
    }

    public async Task<PerformanceReport> GenerateReportAsync(DateTime from, DateTime to)
    {
        try
        {
            var report = new PerformanceReport
            {
                From = from,
                To = to,
                GeneratedAt = DateTime.UtcNow,
                Metrics = new List<MetricSummary>()
            };

            // Get metrics from Redis cache
            var pattern = $"metrics:*";
            var keys = await _cache.GetKeysAsync(pattern);
            
            foreach (var key in keys)
            {
                var parts = key.Split(':');
                if (parts.Length >= 2)
                {
                    var metricName = parts[1];
                    var value = await _cache.GetAsync<long?>(key);
                    
                    if (value.HasValue)
                    {
                        report.Metrics.Add(new MetricSummary
                        {
                            Name = metricName,
                            Value = value.Value,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
            return new PerformanceReport
            {
                From = from,
                To = to,
                GeneratedAt = DateTime.UtcNow,
                Metrics = new List<MetricSummary>()
            };
        }
    }

    public void TrackApiEndpoint(string endpoint, string method, int statusCode, TimeSpan duration)
    {
        var properties = new Dictionary<string, string>
        {
            ["Endpoint"] = endpoint,
            ["Method"] = method,
            ["StatusCode"] = statusCode.ToString(),
            ["Success"] = (statusCode < 400).ToString()
        };

        TrackMetric($"API.{method}.Duration", duration.TotalMilliseconds, properties);
        TrackMetric($"API.{method}.Requests", 1, properties);
        
        if (statusCode >= 400)
        {
            TrackMetric($"API.{method}.Errors", 1, properties);
        }
    }

    public void TrackDatabaseQuery(string queryType, TimeSpan duration, bool success)
    {
        var properties = new Dictionary<string, string>
        {
            ["QueryType"] = queryType,
            ["Success"] = success.ToString()
        };

        TrackMetric($"Database.{queryType}.Duration", duration.TotalMilliseconds, properties);
        TrackMetric($"Database.{queryType}.Queries", 1, properties);
        
        if (!success)
        {
            TrackMetric($"Database.{queryType}.Errors", 1, properties);
        }
    }

    private class OperationTracker : IDisposable
    {
        private readonly string _operationName;
        private readonly Dictionary<string, string> _properties;
        private readonly PerformanceTracker _tracker;
        private readonly Stopwatch _stopwatch;

        public OperationTracker(string operationName, Dictionary<string, string> properties, PerformanceTracker tracker)
        {
            _operationName = operationName;
            _properties = properties ?? new Dictionary<string, string>();
            _tracker = tracker;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _tracker.TrackMetric($"Operation.{_operationName}.Duration", _stopwatch.ElapsedMilliseconds, _properties);
        }
    }
}

public class PerformanceReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<MetricSummary> Metrics { get; set; }
}

public class MetricSummary
{
    public string Name { get; set; }
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
```

---

## 8.2 Structured Logging

Configuração avançada de logging estruturado com Serilog e enrichers customizados.

#### IDE.Infrastructure/Logging/LoggingConfiguration.cs
```csharp
public static class LoggingConfiguration
{
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.With<UserContextEnricher>()
            .Enrich.With<WorkspaceContextEnricher>()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
                "| UserId: {UserId} | WorkspaceId: {WorkspaceId} | CorrelationId: {CorrelationId} " +
                "{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/ide-api-.json",
                rollingInterval: RollingInterval.Day,
                formatter: new JsonFormatter(),
                retainedFileCountLimit: 30)
            .WriteTo.ApplicationInsights(
                configuration.GetValue<string>("ApplicationInsights:ConnectionString"),
                TelemetryConverter.Traces)
            .CreateLogger();

        // Replace default logger factory
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger);
        });

        // Custom logging services
        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddSingleton<ILogEnricher, PerformanceLogEnricher>();

        return services;
    }
}

public class UserContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            var userPlan = httpContext.User.FindFirst("plan")?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
            }
            
            if (!string.IsNullOrEmpty(username))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Username", username));
            }
            
            if (!string.IsNullOrEmpty(userPlan))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserPlan", userPlan));
            }
        }
    }
}

public class WorkspaceContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WorkspaceContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var workspaceId = httpContext.Request.Headers["X-Workspace-Id"].FirstOrDefault() ??
                             httpContext.Request.Query["workspaceId"].FirstOrDefault() ??
                             httpContext.Items["WorkspaceId"]?.ToString();

            if (!string.IsNullOrEmpty(workspaceId))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("WorkspaceId", workspaceId));
            }
        }
    }
}

public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var correlationId = httpContext.TraceIdentifier;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }
    }
}
```

### Activity and Audit Logging

#### IDE.Infrastructure/Logging/IActivityLogger.cs
```csharp
public interface IActivityLogger
{
    Task LogUserActivityAsync(Guid userId, string activity, object details = null);
    Task LogWorkspaceActivityAsync(Guid workspaceId, Guid userId, string activity, object details = null);
    Task LogSystemActivityAsync(string activity, object details = null);
    Task<List<ActivityLog>> GetUserActivitiesAsync(Guid userId, DateTime? from = null, int count = 100);
    Task<List<ActivityLog>> GetWorkspaceActivitiesAsync(Guid workspaceId, DateTime? from = null, int count = 100);
}

public class ActivityLogger : IActivityLogger
{
    private readonly ApplicationDbContext _context;
    private readonly ICustomTelemetryClient _telemetryClient;
    private readonly ILogger<ActivityLogger> _logger;

    public ActivityLogger(
        ApplicationDbContext context, 
        ICustomTelemetryClient telemetryClient, 
        ILogger<ActivityLogger> logger)
    {
        _context = context;
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public async Task LogUserActivityAsync(Guid userId, string activity, object details = null)
    {
        try
        {
            var activityLog = new ActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Activity = activity,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetCurrentUserIpAddress(),
                UserAgent = GetCurrentUserAgent()
            };

            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            // Track in Application Insights
            _telemetryClient.TrackUserAction(activity, userId, new Dictionary<string, string>
            {
                ["Details"] = activityLog.Details ?? "",
                ["IpAddress"] = activityLog.IpAddress ?? "",
                ["UserAgent"] = activityLog.UserAgent ?? ""
            });

            _logger.LogInformation("User activity logged: {Activity} for user {UserId}", activity, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging user activity: {Activity} for user {UserId}", activity, userId);
        }
    }

    public async Task LogWorkspaceActivityAsync(Guid workspaceId, Guid userId, string activity, object details = null)
    {
        try
        {
            var activityLog = new ActivityLog
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Activity = activity,
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetCurrentUserIpAddress(),
                UserAgent = GetCurrentUserAgent()
            };

            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            // Track in Application Insights
            var properties = new Dictionary<string, string>
            {
                ["WorkspaceId"] = workspaceId.ToString(),
                ["Details"] = activityLog.Details ?? "",
                ["IpAddress"] = activityLog.IpAddress ?? ""
            };

            _telemetryClient.TrackUserAction($"Workspace.{activity}", userId, properties);

            _logger.LogInformation("Workspace activity logged: {Activity} for workspace {WorkspaceId} by user {UserId}", 
                activity, workspaceId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging workspace activity: {Activity} for workspace {WorkspaceId}", 
                activity, workspaceId);
        }
    }

    public async Task LogSystemActivityAsync(string activity, object details = null)
    {
        try
        {
            var activityLog = new ActivityLog
            {
                Id = Guid.NewGuid(),
                Activity = $"System.{activity}",
                Details = details != null ? JsonSerializer.Serialize(details) : null,
                Timestamp = DateTime.UtcNow
            };

            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();

            // Track in Application Insights
            _telemetryClient.TrackCustomEvent($"System.{activity}", new Dictionary<string, string>
            {
                ["Details"] = activityLog.Details ?? ""
            });

            _logger.LogInformation("System activity logged: {Activity}", activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging system activity: {Activity}", activity);
        }
    }

    public async Task<List<ActivityLog>> GetUserActivitiesAsync(Guid userId, DateTime? from = null, int count = 100)
    {
        try
        {
            var query = _context.ActivityLogs
                .Where(al => al.UserId == userId);

            if (from.HasValue)
            {
                query = query.Where(al => al.Timestamp >= from.Value);
            }

            return await query
                .OrderByDescending(al => al.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user activities for user {UserId}", userId);
            return new List<ActivityLog>();
        }
    }

    public async Task<List<ActivityLog>> GetWorkspaceActivitiesAsync(Guid workspaceId, DateTime? from = null, int count = 100)
    {
        try
        {
            var query = _context.ActivityLogs
                .Where(al => al.WorkspaceId == workspaceId);

            if (from.HasValue)
            {
                query = query.Where(al => al.Timestamp >= from.Value);
            }

            return await query
                .OrderByDescending(al => al.Timestamp)
                .Take(count)
                .Include(al => al.User)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspace activities for workspace {WorkspaceId}", workspaceId);
            return new List<ActivityLog>();
        }
    }

    private string GetCurrentUserIpAddress()
    {
        // Implementation depends on your HTTP context setup
        return "Unknown";
    }

    private string GetCurrentUserAgent()
    {
        // Implementation depends on your HTTP context setup
        return "Unknown";
    }
}
```

---

## 8.3 SLA Monitoring

Sistema de monitoramento de SLA com alertas automáticos e relatórios.

#### IDE.Infrastructure/Monitoring/ISlaMonitoring.cs
```csharp
public interface ISlaMonitoring
{
    Task RecordUptimeAsync(bool isHealthy);
    Task<SlaMetrics> GetCurrentSlaAsync();
    Task<SlaReport> GenerateMonthlySlaReportAsync(DateTime month);
    Task SendSlaAlertAsync(double currentUptime, double target);
    Task<List<DowntimeIncident>> GetRecentIncidentsAsync(int days = 30);
}

public class SlaMonitoring : ISlaMonitoring
{
    private readonly IRedisCacheService _cache;
    private readonly ICustomTelemetryClient _telemetryClient;
    private readonly ILogger<SlaMonitoring> _logger;
    private readonly INotificationService _notificationService;

    public SlaMonitoring(
        IRedisCacheService cache,
        ICustomTelemetryClient telemetryClient,
        ILogger<SlaMonitoring> logger,
        INotificationService notificationService)
    {
        _cache = cache;
        _telemetryClient = telemetryClient;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task RecordUptimeAsync(bool isHealthy)
    {
        var timestamp = DateTime.UtcNow;
        var hourlyKey = $"sla:uptime:{timestamp:yyyyMMddHH}";
        var dailyKey = $"sla:uptime:daily:{timestamp:yyyyMMdd}";
        var monthlyKey = $"sla:uptime:monthly:{timestamp:yyyyMM}";

        try
        {
            var uptimeData = await _cache.GetAsync<UptimeRecord>(hourlyKey) ?? new UptimeRecord();
            
            uptimeData.TotalChecks++;
            if (isHealthy)
            {
                uptimeData.HealthyChecks++;
            }
            else
            {
                uptimeData.UnhealthyChecks++;
                _logger.LogWarning("Health check failed at {Timestamp}", timestamp);
                
                // Record downtime incident
                await RecordDowntimeIncidentAsync(timestamp);
            }

            // Store hourly data
            await _cache.SetAsync(hourlyKey, uptimeData, TimeSpan.FromDays(32));
            
            // Aggregate daily data
            await UpdateDailyAggregateAsync(dailyKey, isHealthy);
            
            // Aggregate monthly data
            await UpdateMonthlyAggregateAsync(monthlyKey, isHealthy);

            // Send to Application Insights
            _telemetryClient.TrackMetric("SLA.HealthCheck", isHealthy ? 1 : 0);
            _telemetryClient.TrackMetric("SLA.Uptime.TotalChecks", 1);
            
            if (isHealthy)
            {
                _telemetryClient.TrackMetric("SLA.Uptime.HealthyChecks", 1);
            }
            else
            {
                _telemetryClient.TrackMetric("SLA.Uptime.UnhealthyChecks", 1);
            }
            
            // Check if SLA is at risk
            var currentSla = await GetCurrentSlaAsync();
            if (currentSla.UptimePercentage < 99.5) // Alert before SLA breach
            {
                await SendSlaAlertAsync(currentSla.UptimePercentage, 99.9);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording uptime data");
        }
    }

    public async Task<SlaMetrics> GetCurrentSlaAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthlyKey = $"sla:uptime:monthly:{now:yyyyMM}";

            var monthlyData = await _cache.GetAsync<UptimeRecord>(monthlyKey);
            if (monthlyData == null)
            {
                return new SlaMetrics
                {
                    Period = "Current Month",
                    StartDate = monthStart,
                    EndDate = now,
                    UptimePercentage = 100.0,
                    TotalChecks = 0,
                    HealthyChecks = 0,
                    UnhealthyChecks = 0
                };
            }

            var uptimePercentage = monthlyData.TotalChecks > 0 
                ? (double)monthlyData.HealthyChecks / monthlyData.TotalChecks * 100
                : 100.0;

            return new SlaMetrics
            {
                Period = "Current Month",
                StartDate = monthStart,
                EndDate = now,
                UptimePercentage = uptimePercentage,
                TotalChecks = monthlyData.TotalChecks,
                HealthyChecks = monthlyData.HealthyChecks,
                UnhealthyChecks = monthlyData.UnhealthyChecks
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current SLA metrics");
            return new SlaMetrics { UptimePercentage = 0 };
        }
    }

    public async Task<SlaReport> GenerateMonthlySlaReportAsync(DateTime month)
    {
        try
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthlyKey = $"sla:uptime:monthly:{month:yyyyMM}";

            var monthlyData = await _cache.GetAsync<UptimeRecord>(monthlyKey);
            var incidents = await GetIncidentsForPeriodAsync(monthStart, monthEnd);

            var report = new SlaReport
            {
                Month = month,
                Period = $"{monthStart:MMMM yyyy}",
                StartDate = monthStart,
                EndDate = monthEnd,
                GeneratedAt = DateTime.UtcNow
            };

            if (monthlyData != null)
            {
                report.UptimePercentage = monthlyData.TotalChecks > 0 
                    ? (double)monthlyData.HealthyChecks / monthlyData.TotalChecks * 100
                    : 100.0;
                report.TotalChecks = monthlyData.TotalChecks;
                report.HealthyChecks = monthlyData.HealthyChecks;
                report.UnhealthyChecks = monthlyData.UnhealthyChecks;
            }

            report.Incidents = incidents;
            report.TotalDowntimeMinutes = incidents.Sum(i => i.DurationMinutes);
            report.IncidentCount = incidents.Count;

            // Calculate SLA compliance
            report.SlaTarget = 99.9;
            report.SlaCompliance = report.UptimePercentage >= report.SlaTarget;

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating monthly SLA report for {Month}", month);
            return new SlaReport
            {
                Month = month,
                GeneratedAt = DateTime.UtcNow,
                UptimePercentage = 0
            };
        }
    }

    public async Task SendSlaAlertAsync(double currentUptime, double target)
    {
        try
        {
            var alert = new SlaAlert
            {
                Timestamp = DateTime.UtcNow,
                CurrentUptime = currentUptime,
                TargetUptime = target,
                Severity = currentUptime < 99.0 ? AlertSeverity.Critical : AlertSeverity.Warning,
                Message = $"SLA at risk: Current uptime is {currentUptime:F2}%, target is {target:F1}%"
            };

            // Send notification
            await _notificationService.SendSlaAlertAsync(alert);

            // Log alert
            _logger.LogWarning("SLA alert sent: {Message}", alert.Message);

            // Track in Application Insights
            _telemetryClient.TrackCustomEvent("SLA.Alert", new Dictionary<string, string>
            {
                ["CurrentUptime"] = currentUptime.ToString("F2"),
                ["TargetUptime"] = target.ToString("F1"),
                ["Severity"] = alert.Severity.ToString(),
                ["Message"] = alert.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SLA alert");
        }
    }

    public async Task<List<DowntimeIncident>> GetRecentIncidentsAsync(int days = 30)
    {
        try
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            return await GetIncidentsForPeriodAsync(fromDate, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent incidents");
            return new List<DowntimeIncident>();
        }
    }

    private async Task UpdateDailyAggregateAsync(string dailyKey, bool isHealthy)
    {
        var dailyData = await _cache.GetAsync<UptimeRecord>(dailyKey) ?? new UptimeRecord();
        dailyData.TotalChecks++;
        
        if (isHealthy)
            dailyData.HealthyChecks++;
        else
            dailyData.UnhealthyChecks++;

        await _cache.SetAsync(dailyKey, dailyData, TimeSpan.FromDays(32));
    }

    private async Task UpdateMonthlyAggregateAsync(string monthlyKey, bool isHealthy)
    {
        var monthlyData = await _cache.GetAsync<UptimeRecord>(monthlyKey) ?? new UptimeRecord();
        monthlyData.TotalChecks++;
        
        if (isHealthy)
            monthlyData.HealthyChecks++;
        else
            monthlyData.UnhealthyChecks++;

        await _cache.SetAsync(monthlyKey, monthlyData, TimeSpan.FromDays(400)); // Keep for over a year
    }

    private async Task RecordDowntimeIncidentAsync(DateTime timestamp)
    {
        var incidentKey = $"incident:{timestamp:yyyyMMddHHmm}";
        var incident = new DowntimeIncident
        {
            Id = Guid.NewGuid(),
            StartTime = timestamp,
            Detected = true,
            Severity = DetermineSeverity(timestamp)
        };

        await _cache.SetAsync(incidentKey, incident, TimeSpan.FromDays(90));
    }

    private async Task<List<DowntimeIncident>> GetIncidentsForPeriodAsync(DateTime from, DateTime to)
    {
        var incidents = new List<DowntimeIncident>();
        var pattern = "incident:*";
        var keys = await _cache.GetKeysAsync(pattern);

        foreach (var key in keys)
        {
            var incident = await _cache.GetAsync<DowntimeIncident>(key);
            if (incident != null && incident.StartTime >= from && incident.StartTime <= to)
            {
                incidents.Add(incident);
            }
        }

        return incidents.OrderByDescending(i => i.StartTime).ToList();
    }

    private IncidentSeverity DetermineSeverity(DateTime timestamp)
    {
        // Logic to determine severity based on various factors
        return IncidentSeverity.Medium;
    }
}

// Supporting models
public class UptimeRecord
{
    public long TotalChecks { get; set; }
    public long HealthyChecks { get; set; }
    public long UnhealthyChecks { get; set; }
}

public class SlaMetrics
{
    public string Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double UptimePercentage { get; set; }
    public long TotalChecks { get; set; }
    public long HealthyChecks { get; set; }
    public long UnhealthyChecks { get; set; }
}

public class SlaReport : SlaMetrics
{
    public DateTime Month { get; set; }
    public DateTime GeneratedAt { get; set; }
    public double SlaTarget { get; set; }
    public bool SlaCompliance { get; set; }
    public List<DowntimeIncident> Incidents { get; set; } = new();
    public double TotalDowntimeMinutes { get; set; }
    public int IncidentCount { get; set; }
}

public class DowntimeIncident
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double DurationMinutes => EndTime.HasValue ? (EndTime.Value - StartTime).TotalMinutes : 0;
    public bool Detected { get; set; }
    public IncidentSeverity Severity { get; set; }
    public string Description { get; set; }
}

public class SlaAlert
{
    public DateTime Timestamp { get; set; }
    public double CurrentUptime { get; set; }
    public double TargetUptime { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; }
}

public enum IncidentSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
```

---

## Entregáveis da Parte 9

### ✅ Implementações Completas
- **Application Insights** integration completa
- **Custom Telemetry Client** com métricas específicas
- **Structured Logging** com Serilog e enrichers
- **Performance Tracking** detalhado
- **Activity and Audit Logging** completo
- **SLA Monitoring** com alertas automáticos

### ✅ Funcionalidades de Monitoramento
- **Real-time telemetry** para todas as operações
- **User and workspace** context enrichment
- **Performance metrics** agregadas
- **Uptime monitoring** com histórico
- **Incident tracking** automático
- **SLA reporting** mensal

### ✅ Observabilidade Avançada
- **Correlation IDs** para rastreamento
- **Custom metrics** para business logic
- **Exception tracking** detalhado
- **Dependency tracking** automático
- **User activity logging** completo
- **System health monitoring** contínuo

---

## Validação da Parte 9

### Critérios de Sucesso
- [ ] Application Insights recebe telemetria
- [ ] Logs estruturados são gerados corretamente
- [ ] Métricas customizadas são coletadas
- [ ] SLA monitoring funciona adequadamente
- [ ] Alertas são enviados quando necessário
- [ ] Performance tracking está funcionando
- [ ] Activity logs são persistidos

### Testes de Monitoramento
```bash
# 1. Verificar logs estruturados
tail -f logs/ide-api-.json | jq .

# 2. Testar métricas customizadas
curl -X GET http://localhost:8503/api/workspaces -H "Authorization: Bearer <token>"

# 3. Verificar Application Insights
# Acessar Azure Portal > Application Insights > Live Metrics

# 4. Testar alertas de SLA
# Simular downtime e verificar notificações

# 5. Verificar activity logs
curl -X GET http://localhost:8503/api/admin/activity-logs/<user-id> -H "Authorization: Bearer <admin-token>"
```

### Monitoring Targets
- **Telemetry data**: 100% das operações
- **Log retention**: 30 dias mínimo
- **SLA target**: 99.9% uptime
- **Alert response**: < 5 minutos
- **Performance tracking**: < 1% overhead
- **Incident detection**: < 2 minutos

---

## Próximos Passos

Após validação da Parte 9, prosseguir para:
- **Parte 10**: Performance Tuning & Final Validation

---

**Tempo Estimado**: 3-4 horas  
**Complexidade**: Alta  
**Dependências**: Application Insights, Serilog, Redis  
**Entregável**: Sistema completo de monitoramento e observabilidade em produção