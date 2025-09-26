# Parte 11: Infraestrutura, Middleware e Logging

> **Tempo estimado:** 45-60 minutos  
> **Pré-requisitos:** Partes 1-10 concluídas  
> **Etapa:** Configuração da infraestrutura base

## Objetivos

✅ Configurar Redis para cache  
✅ Implementar Serilog para logging estruturado  
✅ Criar middleware pipeline  
✅ Configurar health checks  
✅ Implementar rate limiting  

## 1. Configuração do Redis

### 1.1. Configurar Redis Connection

**`src/Api/Extensions/RedisExtensions.cs`**
```csharp
using StackExchange.Redis;

namespace Vanq.Backend.Api.Extensions
{
    public static class RedisExtensions
    {
        public static IServiceCollection AddRedisConfiguration(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Redis");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                services.AddMemoryCache(); // Fallback para desenvolvimento
                return services;
            }

            try
            {
                services.AddSingleton<IConnectionMultiplexer>(provider =>
                {
                    var config = ConfigurationOptions.Parse(connectionString);
                    config.ConnectTimeout = 5000;
                    config.CommandTimeout = 5000;
                    config.SyncTimeout = 5000;
                    config.ConnectRetry = 3;
                    config.ReconnectRetryPolicy = new ExponentialRetry(1000);
                    config.AbortOnConnectFail = false;
                    
                    var connection = ConnectionMultiplexer.Connect(config);
                    
                    // Log de conexão
                    var logger = provider.GetRequiredService<ILogger<IConnectionMultiplexer>>();
                    logger.LogInformation("Redis connected: {Status}", connection.IsConnected);
                    
                    return connection;
                });

                services.AddSingleton<IDatabase>(provider =>
                {
                    var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
                    return multiplexer.GetDatabase();
                });

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = connectionString;
                    options.InstanceName = "VanqBackend";
                });
            }
            catch (Exception ex)
            {
                // Log erro e fallback para memory cache
                var logger = services.BuildServiceProvider().GetService<ILogger<IServiceCollection>>();
                logger?.LogError(ex, "Erro ao configurar Redis. Usando MemoryCache como fallback");
                services.AddMemoryCache();
            }

            return services;
        }
    }
}
```

### 1.2. Cache Service

**`src/Application/Interfaces/ICacheService.cs`**
```csharp
namespace Vanq.Backend.Application.Interfaces
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task SetStringAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
        Task<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default);
        Task RefreshAsync(string key, CancellationToken cancellationToken = default);
    }
}
```

**`src/Infrastructure/Services/CacheService.cs`**
```csharp
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using Vanq.Backend.Application.Interfaces;

namespace Vanq.Backend.Infrastructure.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IDatabase? _database;
        private readonly ILogger<CacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CacheService(
            IDistributedCache distributedCache,
            IDatabase? database,
            ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache;
            _database = database;
            _logger = logger;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var cachedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
                
                if (string.IsNullOrEmpty(cachedValue))
                    return default;

                return JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar cache para chave: {Key}", key);
                return default;
            }
        }

        public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _distributedCache.GetStringAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar cache string para chave: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.SetSlidingExpiration(expiration.Value);
                }
                else
                {
                    options.SetSlidingExpiration(TimeSpan.FromMinutes(30)); // Default 30 min
                }

                await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
                
                _logger.LogDebug("Cache definido para chave: {Key}, Expiração: {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir cache para chave: {Key}", key);
            }
        }

        public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var options = new DistributedCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.SetSlidingExpiration(expiration.Value);
                }
                else
                {
                    options.SetSlidingExpiration(TimeSpan.FromMinutes(30));
                }

                await _distributedCache.SetStringAsync(key, value, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir cache string para chave: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                _logger.LogDebug("Cache removido para chave: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover cache para chave: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_database == null)
                {
                    _logger.LogWarning("Redis database não disponível para remover por padrão: {Pattern}", pattern);
                    return;
                }

                var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: pattern);

                var tasks = keys.Select(key => _database.KeyDeleteAsync(key));
                await Task.WhenAll(tasks);

                _logger.LogDebug("Cache removido por padrão: {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover cache por padrão: {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var cachedValue = await _distributedCache.GetAsync(key, cancellationToken);
                return cachedValue != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar existência do cache para chave: {Key}", key);
                return false;
            }
        }

        public async Task<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_database == null)
                    return null;

                var ttl = await _database.KeyTimeToLiveAsync(key);
                return ttl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter TTL para chave: {Key}", key);
                return null;
            }
        }

        public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _distributedCache.RefreshAsync(key, cancellationToken);
                _logger.LogDebug("Cache atualizado para chave: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar cache para chave: {Key}", key);
            }
        }
    }
}
```

## 2. Configuração do Serilog

### 2.1. Serilog Setup

**`src/Api/Extensions/SerilogExtensions.cs`**
```csharp
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Formatting.Json;
using Serilog.Sinks.SystemConsole.Themes;

namespace Vanq.Backend.Api.Extensions
{
    public static class SerilogExtensions
    {
        public static void ConfigureSerilog(this WebApplicationBuilder builder)
        {
            var configuration = builder.Configuration;
            var environment = builder.Environment;

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithProcessName()
                .Enrich.WithProperty("Application", "VanqBackend")
                .Enrich.WithProperty("Version", GetVersion())
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.StaticFiles"))
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Hosting.Diagnostics"))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Information)
                    .WriteTo.Console(
                        theme: AnsiConsoleTheme.Code,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                )
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Warning)
                    .WriteTo.File(
                        path: "logs/vanq-backend-.log",
                        formatter: new JsonFormatter(),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        buffered: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1))
                )
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.File(
                        path: "logs/errors/vanq-backend-errors-.log",
                        formatter: new JsonFormatter(),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90,
                        buffered: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1))
                )
                .CreateLogger();

            builder.Host.UseSerilog();
        }

        private static string GetVersion()
        {
            var assembly = typeof(SerilogExtensions).Assembly;
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
    }
}
```

### 2.2. Structured Logging Models

**`src/Application/Common/Logging/LoggingEvents.cs`**
```csharp
namespace Vanq.Backend.Application.Common.Logging
{
    public static class LoggingEvents
    {
        // Authentication Events (1000-1999)
        public const int UserLogin = 1001;
        public const int UserLoginFailed = 1002;
        public const int UserLogout = 1003;
        public const int UserRegistration = 1004;
        public const int UserRegistrationFailed = 1005;
        public const int PasswordReset = 1006;
        public const int TwoFactorEnabled = 1007;
        public const int TwoFactorDisabled = 1008;
        public const int TwoFactorFailed = 1009;
        public const int AccountLocked = 1010;
        public const int AccountUnlocked = 1011;
        public const int RefreshTokenUsed = 1012;
        public const int RefreshTokenRevoked = 1013;

        // OAuth Events (2000-2999)
        public const int OAuthLogin = 2001;
        public const int OAuthLoginFailed = 2002;
        public const int OAuthAccountLinked = 2003;
        public const int OAuthAccountUnlinked = 2004;
        public const int OAuthTokenRefresh = 2005;
        public const int OAuthTokenRefreshFailed = 2006;

        // Security Events (3000-3999)
        public const int SecurityThreat = 3001;
        public const int SuspiciousActivity = 3002;
        public const int BruteForceAttempt = 3003;
        public const int RateLimitExceeded = 3004;
        public const int InvalidApiKey = 3005;
        public const int UnauthorizedAccess = 3006;
        public const int DataBreach = 3007;

        // Email Events (4000-4999)
        public const int EmailSent = 4001;
        public const int EmailFailed = 4002;
        public const int EmailQueued = 4003;
        public const int EmailTemplateRendered = 4004;
        public const int EmailProviderChanged = 4005;
        public const int BulkEmailSent = 4006;

        // System Events (5000-5999)
        public const int ApplicationStartup = 5001;
        public const int ApplicationShutdown = 5002;
        public const int DatabaseConnection = 5003;
        public const int DatabaseConnectionFailed = 5004;
        public const int CacheHit = 5005;
        public const int CacheMiss = 5006;
        public const int HealthCheckFailed = 5007;
        public const int BackgroundTaskStarted = 5008;
        public const int BackgroundTaskCompleted = 5009;
        public const int BackgroundTaskFailed = 5010;

        // API Events (6000-6999)
        public const int ApiRequestStarted = 6001;
        public const int ApiRequestCompleted = 6002;
        public const int ApiRequestFailed = 6003;
        public const int ApiValidationFailed = 6004;
        public const int ApiRateLimited = 6005;
        public const int ApiVersionMismatch = 6006;
    }
}
```

**`src/Application/Common/Logging/LoggingExtensions.cs`**
```csharp
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Vanq.Backend.Application.Common.Logging
{
    public static class LoggingExtensions
    {
        public static void LogUserLogin(this ILogger logger, string email, string ipAddress, string userAgent)
        {
            logger.LogInformation(LoggingEvents.UserLogin, 
                "Usuário {Email} fez login de {IpAddress} com {UserAgent}", 
                email, ipAddress, userAgent);
        }

        public static void LogUserLoginFailed(this ILogger logger, string email, string ipAddress, string reason)
        {
            logger.LogWarning(LoggingEvents.UserLoginFailed, 
                "Tentativa de login falhou para {Email} de {IpAddress}. Razão: {Reason}", 
                email, ipAddress, reason);
        }

        public static void LogUserLogout(this ILogger logger, string email, string ipAddress)
        {
            logger.LogInformation(LoggingEvents.UserLogout, 
                "Usuário {Email} fez logout de {IpAddress}", 
                email, ipAddress);
        }

        public static void LogSecurityThreat(this ILogger logger, string threatType, string ipAddress, string details)
        {
            logger.LogCritical(LoggingEvents.SecurityThreat, 
                "Ameaça de segurança detectada: {ThreatType} de {IpAddress}. Detalhes: {Details}", 
                threatType, ipAddress, details);
        }

        public static void LogSuspiciousActivity(this ILogger logger, string activity, string userId, string ipAddress, string details)
        {
            logger.LogWarning(LoggingEvents.SuspiciousActivity, 
                "Atividade suspeita detectada: {Activity} para usuário {UserId} de {IpAddress}. Detalhes: {Details}", 
                activity, userId, ipAddress, details);
        }

        public static void LogBruteForceAttempt(this ILogger logger, string email, string ipAddress, int attemptCount)
        {
            logger.LogWarning(LoggingEvents.BruteForceAttempt, 
                "Tentativa de força bruta detectada para {Email} de {IpAddress}. Tentativas: {AttemptCount}", 
                email, ipAddress, attemptCount);
        }

        public static void LogRateLimitExceeded(this ILogger logger, string endpoint, string clientId, string ipAddress)
        {
            logger.LogWarning(LoggingEvents.RateLimitExceeded, 
                "Rate limit excedido para endpoint {Endpoint} pelo cliente {ClientId} de {IpAddress}", 
                endpoint, clientId, ipAddress);
        }

        public static void LogEmailSent(this ILogger logger, string to, string subject, string provider, TimeSpan duration)
        {
            logger.LogInformation(LoggingEvents.EmailSent, 
                "Email enviado para {To} com assunto '{Subject}' via {Provider} em {Duration}ms", 
                to, subject, provider, duration.TotalMilliseconds);
        }

        public static void LogEmailFailed(this ILogger logger, string to, string subject, string provider, Exception exception)
        {
            logger.LogError(LoggingEvents.EmailFailed, exception, 
                "Falha ao enviar email para {To} com assunto '{Subject}' via {Provider}", 
                to, subject, provider);
        }

        public static void LogApiRequest(this ILogger logger, string method, string path, int statusCode, TimeSpan duration, string? userId = null, string? ipAddress = null)
        {
            var eventId = statusCode >= 400 ? LoggingEvents.ApiRequestFailed : LoggingEvents.ApiRequestCompleted;
            
            logger.LogInformation(eventId, 
                "API {Method} {Path} respondeu {StatusCode} em {Duration}ms para usuário {UserId} de {IpAddress}", 
                method, path, statusCode, duration.TotalMilliseconds, userId ?? "anonymous", ipAddress ?? "unknown");
        }

        public static void LogCacheOperation(this ILogger logger, string operation, string key, bool hit, TimeSpan? duration = null)
        {
            var eventId = hit ? LoggingEvents.CacheHit : LoggingEvents.CacheMiss;
            
            if (duration.HasValue)
            {
                logger.LogDebug(eventId, 
                    "Cache {Operation} para chave {Key}: {Result} em {Duration}ms", 
                    operation, key, hit ? "HIT" : "MISS", duration.Value.TotalMilliseconds);
            }
            else
            {
                logger.LogDebug(eventId, 
                    "Cache {Operation} para chave {Key}: {Result}", 
                    operation, key, hit ? "HIT" : "MISS");
            }
        }

        public static void LogHealthCheck(this ILogger logger, string checkName, bool isHealthy, TimeSpan duration, string? message = null)
        {
            var level = isHealthy ? LogLevel.Information : LogLevel.Error;
            var eventId = isHealthy ? 0 : LoggingEvents.HealthCheckFailed;
            
            logger.Log(level, eventId, 
                "Health check {CheckName}: {Status} em {Duration}ms. {Message}", 
                checkName, isHealthy ? "HEALTHY" : "UNHEALTHY", duration.TotalMilliseconds, message ?? "");
        }

        public static IDisposable? BeginScopeWith(this ILogger logger, params (string key, object? value)[] properties)
        {
            var dict = properties.ToDictionary(p => p.key, p => p.value);
            return logger.BeginScope(dict);
        }
    }
}
```

## 3. Middleware Pipeline

### 3.1. Request Logging Middleware

**`src/Api/Middleware/RequestLoggingMiddleware.cs`**
```csharp
using Serilog.Context;
using System.Diagnostics;
using System.Security.Claims;
using Vanq.Backend.Application.Common.Logging;

namespace Vanq.Backend.Api.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly HashSet<string> _sensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "X-API-Key", "X-Auth-Token", "Set-Cookie"
        };

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N")[..8];
            
            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("IpAddress", GetClientIpAddress(context)))
            using (LogContext.PushProperty("UserAgent", context.Request.Headers.UserAgent.ToString()))
            {
                var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    LogContext.PushProperty("UserId", userId);
                }

                // Log início da request
                _logger.LogInformation(LoggingEvents.ApiRequestStarted,
                    "Iniciando {Method} {Path}{QueryString}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString);

                try
                {
                    await _next(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(LoggingEvents.ApiRequestFailed, ex,
                        "Erro não tratado na request {Method} {Path}",
                        context.Request.Method,
                        context.Request.Path);
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    
                    _logger.LogApiRequest(
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        stopwatch.Elapsed,
                        userId,
                        GetClientIpAddress(context)
                    );
                }
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Verifica headers de proxy
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
```

### 3.2. Error Handling Middleware

**`src/Api/Middleware/ErrorHandlingMiddleware.cs`**
```csharp
using System.Net;
using System.Text.Json;
using Vanq.Backend.Application.Common.Exceptions;
using Vanq.Backend.Application.Common.Logging;
using Vanq.Backend.Shared.Common;

namespace Vanq.Backend.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public ErrorHandlingMiddleware(
            RequestDelegate next, 
            ILogger<ErrorHandlingMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var errorId = Guid.NewGuid().ToString("N")[..8];
            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ApiResponse<object>
            {
                Success = false,
                ErrorId = errorId,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case ValidationException validationEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Dados de entrada inválidos";
                    errorResponse.Errors = validationEx.Errors.Select(e => new ApiError
                    {
                        Code = "VALIDATION_ERROR",
                        Message = e.ErrorMessage,
                        Field = e.PropertyName
                    }).ToList();
                    
                    _logger.LogWarning(LoggingEvents.ApiValidationFailed,
                        "Validation failed for {RequestPath}. ErrorId: {ErrorId}. Errors: {@Errors}",
                        context.Request.Path, errorId, validationEx.Errors);
                    break;

                case UnauthorizedException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Acesso não autorizado";
                    errorResponse.Errors = new List<ApiError> 
                    { 
                        new() { Code = "UNAUTHORIZED", Message = "Token de acesso inválido ou expirado" } 
                    };
                    
                    _logger.LogWarning("Unauthorized access attempt for {RequestPath}. ErrorId: {ErrorId}",
                        context.Request.Path, errorId);
                    break;

                case ForbiddenException:
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    errorResponse.Message = "Acesso proibido";
                    errorResponse.Errors = new List<ApiError> 
                    { 
                        new() { Code = "FORBIDDEN", Message = "Permissões insuficientes para esta operação" } 
                    };
                    
                    _logger.LogWarning("Forbidden access attempt for {RequestPath}. ErrorId: {ErrorId}",
                        context.Request.Path, errorId);
                    break;

                case NotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "Recurso não encontrado";
                    errorResponse.Errors = new List<ApiError> 
                    { 
                        new() { Code = "NOT_FOUND", Message = exception.Message } 
                    };
                    
                    _logger.LogWarning("Resource not found for {RequestPath}. ErrorId: {ErrorId}. Message: {Message}",
                        context.Request.Path, errorId, exception.Message);
                    break;

                case ConflictException:
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    errorResponse.Message = "Conflito de dados";
                    errorResponse.Errors = new List<ApiError> 
                    { 
                        new() { Code = "CONFLICT", Message = exception.Message } 
                    };
                    
                    _logger.LogWarning("Data conflict for {RequestPath}. ErrorId: {ErrorId}. Message: {Message}",
                        context.Request.Path, errorId, exception.Message);
                    break;

                case BusinessRuleException businessEx:
                    response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                    errorResponse.Message = "Regra de negócio violada";
                    errorResponse.Errors = new List<ApiError> 
                    { 
                        new() { Code = businessEx.Code, Message = businessEx.Message } 
                    };
                    
                    _logger.LogWarning("Business rule violation for {RequestPath}. ErrorId: {ErrorId}. Code: {Code}, Message: {Message}",
                        context.Request.Path, errorId, businessEx.Code, businessEx.Message);
                    break;

                case RateLimitException:
                    response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    errorResponse.Message = "Muitas requisições";
                    errorResponse.Errors = new List<ApiError> 
                    { 
                        new() { Code = "RATE_LIMIT_EXCEEDED", Message = "Taxa de requisições excedida. Tente novamente mais tarde." } 
                    };
                    
                    _logger.LogRateLimitExceeded(context.Request.Path, 
                        context.User?.Identity?.Name ?? "anonymous",
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "Erro interno do servidor";
                    
                    if (_environment.IsDevelopment())
                    {
                        errorResponse.Errors = new List<ApiError> 
                        { 
                            new() 
                            { 
                                Code = "INTERNAL_ERROR", 
                                Message = exception.Message,
                                Details = exception.StackTrace 
                            } 
                        };
                    }
                    else
                    {
                        errorResponse.Errors = new List<ApiError> 
                        { 
                            new() { Code = "INTERNAL_ERROR", Message = "Ocorreu um erro inesperado" } 
                        };
                    }
                    
                    _logger.LogError(exception, 
                        "Unhandled exception for {RequestPath}. ErrorId: {ErrorId}",
                        context.Request.Path, errorId);
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _environment.IsDevelopment()
            });

            await response.WriteAsync(jsonResponse);
        }
    }
}
```

### 3.3. Rate Limiting Middleware

**`src/Api/Middleware/RateLimitingMiddleware.cs`**
```csharp
using System.Collections.Concurrent;
using System.Net;
using Vanq.Backend.Application.Common.Exceptions;
using Vanq.Backend.Application.Common.Logging;

namespace Vanq.Backend.Api.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly RateLimitOptions _options;
        
        private static readonly ConcurrentDictionary<string, ClientRateLimit> _clients = new();

        public RateLimitingMiddleware(
            RequestDelegate next, 
            ILogger<RateLimitingMiddleware> logger,
            RateLimitOptions options)
        {
            _next = next;
            _logger = logger;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var key = GetClientKey(context);
            var endpoint = GetEndpointKey(context);
            
            if (IsExemptFromRateLimit(context))
            {
                await _next(context);
                return;
            }

            var clientLimit = _clients.GetOrAdd(key, _ => new ClientRateLimit());
            
            if (!clientLimit.TryAcquire(endpoint, _options))
            {
                _logger.LogRateLimitExceeded(
                    context.Request.Path,
                    context.User?.Identity?.Name ?? "anonymous",
                    GetClientIpAddress(context)
                );
                
                // Adiciona headers de rate limit
                context.Response.Headers.Add("X-RateLimit-Limit", _options.MaxRequests.ToString());
                context.Response.Headers.Add("X-RateLimit-Remaining", "0");
                context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.Add(_options.TimeWindow).ToUnixTimeSeconds().ToString());
                
                throw new RateLimitException("Taxa de requisições excedida");
            }

            // Adiciona headers de rate limit
            var remaining = clientLimit.GetRemainingRequests(endpoint, _options);
            context.Response.Headers.Add("X-RateLimit-Limit", _options.MaxRequests.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", remaining.ToString());
            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.Add(_options.TimeWindow).ToUnixTimeSeconds().ToString());

            await _next(context);
        }

        private string GetClientKey(HttpContext context)
        {
            // Prioriza usuário autenticado
            var userId = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Fallback para IP
            return $"ip:{GetClientIpAddress(context)}";
        }

        private string GetEndpointKey(HttpContext context)
        {
            return $"{context.Request.Method}:{context.Request.Path}";
        }

        private bool IsExemptFromRateLimit(HttpContext context)
        {
            // Health checks e endpoints de sistema
            var path = context.Request.Path.Value?.ToLower();
            return path?.StartsWith("/health") == true ||
                   path?.StartsWith("/metrics") == true ||
                   path?.StartsWith("/swagger") == true;
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }

    public class RateLimitOptions
    {
        public int MaxRequests { get; set; } = 100;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);
        public bool EnablePerEndpointLimits { get; set; } = true;
        public Dictionary<string, EndpointRateLimit> EndpointLimits { get; set; } = new();
    }

    public class EndpointRateLimit
    {
        public int MaxRequests { get; set; }
        public TimeSpan TimeWindow { get; set; }
    }

    public class ClientRateLimit
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimestamps = new();

        public bool TryAcquire(string endpoint, RateLimitOptions options)
        {
            var now = DateTime.UtcNow;
            var queue = _requestTimestamps.GetOrAdd(endpoint, _ => new Queue<DateTime>());
            
            lock (queue)
            {
                // Remove requests antigas
                while (queue.Count > 0 && now - queue.Peek() > options.TimeWindow)
                {
                    queue.Dequeue();
                }

                // Verifica limite específico do endpoint
                var limit = options.MaxRequests;
                var window = options.TimeWindow;
                
                if (options.EnablePerEndpointLimits && 
                    options.EndpointLimits.TryGetValue(endpoint, out var endpointLimit))
                {
                    limit = endpointLimit.MaxRequests;
                    window = endpointLimit.TimeWindow;
                }

                if (queue.Count >= limit)
                {
                    return false;
                }

                queue.Enqueue(now);
                return true;
            }
        }

        public int GetRemainingRequests(string endpoint, RateLimitOptions options)
        {
            var queue = _requestTimestamps.GetOrAdd(endpoint, _ => new Queue<DateTime>());
            
            lock (queue)
            {
                var limit = options.MaxRequests;
                if (options.EnablePerEndpointLimits && 
                    options.EndpointLimits.TryGetValue(endpoint, out var endpointLimit))
                {
                    limit = endpointLimit.MaxRequests;
                }

                return Math.Max(0, limit - queue.Count);
            }
        }
    }

    public class RateLimitException : Exception
    {
        public RateLimitException(string message) : base(message) { }
    }
}
```

## 4. Health Checks

### 4.1. Health Check Extensions

**`src/Api/Extensions/HealthCheckExtensions.cs`**
```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using Vanq.Backend.Infrastructure.Data;

namespace Vanq.Backend.Api.Extensions
{
    public static class HealthCheckExtensions
    {
        public static IServiceCollection AddHealthChecksConfiguration(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy("API está funcionando"))
                .AddDbContext<ApplicationDbContext>(
                    name: "database",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "db", "postgres" })
                .AddRedis(
                    configuration.GetConnectionString("Redis") ?? "",
                    name: "redis",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "cache", "redis" })
                .AddCheck<EmailHealthCheck>(
                    name: "email",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "email", "smtp" })
                .AddCheck<DiskSpaceHealthCheck>(
                    name: "disk-space",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "system", "disk" })
                .AddCheck<MemoryHealthCheck>(
                    name: "memory",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "system", "memory" });

            return services;
        }

        public static WebApplication UseHealthChecksConfiguration(this WebApplication app)
        {
            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse,
                ResultStatusCodes = new Dictionary<HealthStatus, int>
                {
                    [HealthStatus.Healthy] = 200,
                    [HealthStatus.Degraded] = 200,
                    [HealthStatus.Unhealthy] = 503
                }
            });

            app.UseHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = WriteHealthCheckResponse,
                ResultStatusCodes = new Dictionary<HealthStatus, int>
                {
                    [HealthStatus.Healthy] = 200,
                    [HealthStatus.Degraded] = 503,
                    [HealthStatus.Unhealthy] = 503
                }
            });

            app.UseHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = (context, report) => 
                {
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync(JsonSerializer.Serialize(new { status = "healthy" }));
                }
            });

            return app;
        }

        private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                status = report.Status.ToString().ToLower(),
                totalDuration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString().ToLower(),
                    duration = entry.Value.Duration.TotalMilliseconds,
                    exception = entry.Value.Exception?.Message,
                    data = entry.Value.Data.Any() ? entry.Value.Data : null,
                    description = entry.Value.Description,
                    tags = entry.Value.Tags
                }).ToArray(),
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }
    }
}
```

### 4.2. Custom Health Checks

**`src/Infrastructure/HealthChecks/EmailHealthCheck.cs`**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vanq.Backend.Application.Interfaces;

namespace Vanq.Backend.Infrastructure.HealthChecks
{
    public class EmailHealthCheck : IHealthCheck
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailHealthCheck> _logger;

        public EmailHealthCheck(IEmailService emailService, ILogger<EmailHealthCheck> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Verifica se pelo menos um provider está disponível
                var availableProviders = await _emailService.GetAvailableProvidersAsync(cancellationToken);
                
                if (availableProviders.Any())
                {
                    return HealthCheckResult.Healthy($"Email service healthy. Available providers: {string.Join(", ", availableProviders)}");
                }
                
                return HealthCheckResult.Degraded("No email providers available");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email health check failed");
                return HealthCheckResult.Unhealthy("Email service unavailable", ex);
            }
        }
    }
}
```

**`src/Infrastructure/HealthChecks/DiskSpaceHealthCheck.cs`**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vanq.Backend.Infrastructure.HealthChecks
{
    public class DiskSpaceHealthCheck : IHealthCheck
    {
        private readonly ILogger<DiskSpaceHealthCheck> _logger;
        private const long WarningThresholdBytes = 1024 * 1024 * 1024; // 1 GB
        private const long CriticalThresholdBytes = 512 * 1024 * 1024; // 512 MB

        public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
        {
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory())!);
                var availableBytes = drive.AvailableFreeSpace;
                var totalBytes = drive.TotalSize;
                var usedPercentage = ((double)(totalBytes - availableBytes) / totalBytes) * 100;

                var data = new Dictionary<string, object>
                {
                    ["AvailableBytes"] = availableBytes,
                    ["TotalBytes"] = totalBytes,
                    ["UsedPercentage"] = Math.Round(usedPercentage, 2),
                    ["AvailableGB"] = Math.Round(availableBytes / (1024.0 * 1024.0 * 1024.0), 2),
                    ["TotalGB"] = Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2)
                };

                if (availableBytes < CriticalThresholdBytes)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Critical disk space: {availableBytes / (1024 * 1024)} MB available", 
                        data: data);
                }

                if (availableBytes < WarningThresholdBytes)
                {
                    return HealthCheckResult.Degraded(
                        $"Low disk space: {availableBytes / (1024 * 1024)} MB available", 
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"Disk space healthy: {availableBytes / (1024 * 1024)} MB available", 
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disk space health check failed");
                return HealthCheckResult.Unhealthy("Unable to check disk space", ex);
            }
        }
    }
}
```

**`src/Infrastructure/HealthChecks/MemoryHealthCheck.cs`**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace Vanq.Backend.Infrastructure.HealthChecks
{
    public class MemoryHealthCheck : IHealthCheck
    {
        private readonly ILogger<MemoryHealthCheck> _logger;
        private const long WarningThresholdBytes = 500 * 1024 * 1024; // 500 MB
        private const long CriticalThresholdBytes = 1024 * 1024 * 1024; // 1 GB

        public MemoryHealthCheck(ILogger<MemoryHealthCheck> logger)
        {
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;
                var privateMemory = process.PrivateMemorySize64;
                
                // Força garbage collection para obter medição mais precisa
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var totalMemory = GC.GetTotalMemory(false);

                var data = new Dictionary<string, object>
                {
                    ["WorkingSetBytes"] = workingSet,
                    ["PrivateMemoryBytes"] = privateMemory,
                    ["ManagedMemoryBytes"] = totalMemory,
                    ["WorkingSetMB"] = Math.Round(workingSet / (1024.0 * 1024.0), 2),
                    ["PrivateMemoryMB"] = Math.Round(privateMemory / (1024.0 * 1024.0), 2),
                    ["ManagedMemoryMB"] = Math.Round(totalMemory / (1024.0 * 1024.0), 2),
                    ["Generation0Collections"] = GC.CollectionCount(0),
                    ["Generation1Collections"] = GC.CollectionCount(1),
                    ["Generation2Collections"] = GC.CollectionCount(2)
                };

                if (workingSet > CriticalThresholdBytes)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Critical memory usage: {workingSet / (1024 * 1024)} MB", 
                        data: data);
                }

                if (workingSet > WarningThresholdBytes)
                {
                    return HealthCheckResult.Degraded(
                        $"High memory usage: {workingSet / (1024 * 1024)} MB", 
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"Memory usage healthy: {workingSet / (1024 * 1024)} MB", 
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory health check failed");
                return HealthCheckResult.Unhealthy("Unable to check memory usage", ex);
            }
        }
    }
}
```

## 5. Configuração do appsettings.json

### 5.1. appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting.Diagnostics": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.AspNetCore.StaticFiles": "Warning",
        "Microsoft.AspNetCore.Hosting.Diagnostics": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=vanq_backend;Username=postgres;Password=admin123;Port=5432",
    "Redis": "localhost:6379"
  },
  "RateLimit": {
    "MaxRequests": 100,
    "TimeWindowMinutes": 1,
    "EnablePerEndpointLimits": true,
    "EndpointLimits": {
      "POST:/api/auth/login": {
        "MaxRequests": 5,
        "TimeWindowMinutes": 1
      },
      "POST:/api/auth/register": {
        "MaxRequests": 3,
        "TimeWindowMinutes": 5
      },
      "POST:/api/auth/forgot-password": {
        "MaxRequests": 3,
        "TimeWindowMinutes": 15
      }
    }
  },
  "HealthChecks": {
    "CheckIntervalMinutes": 1,
    "TimeoutSeconds": 30
  },
  "AllowedHosts": "*"
}
```

### 5.2. appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.EntityFrameworkCore": "Information"
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=vanq_backend_dev;Username=postgres;Password=admin123;Port=5432",
    "Redis": "localhost:6379"
  },
  "RateLimit": {
    "MaxRequests": 1000,
    "TimeWindowMinutes": 1
  }
}
```

## 6. Program.cs Principal - Configuração Final

**`src/Api/Program.cs`**
```csharp
using Serilog;
using Vanq.Backend.Api.Extensions;
using Vanq.Backend.Api.Middleware;
using Vanq.Backend.Application.Extensions;
using Vanq.Backend.Infrastructure.Extensions;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog
    builder.ConfigureSerilog();

    Log.Information("Iniciando aplicação...");

    // Configurar serviços
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddRedisConfiguration(builder.Configuration);
    builder.Services.AddHealthChecksConfiguration(builder.Configuration);

    // Configurar Rate Limiting
    var rateLimitOptions = new RateLimitOptions();
    builder.Configuration.GetSection("RateLimit").Bind(rateLimitOptions);
    builder.Services.AddSingleton(rateLimitOptions);

    // Configurar API
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Configurar pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Middleware pipeline
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();

    // Configurações padrão
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    // Health checks
    app.UseHealthChecksConfiguration();

    // Endpoints
    app.MapControllers();

    Log.Information("Aplicação iniciada com sucesso em {Environment}", app.Environment.EnvironmentName);
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação falhou ao iniciar");
}
finally
{
    Log.Information("Encerrando aplicação...");
    Log.CloseAndFlush();
}
```

## ✅ Validação dos Passos

### Compilação
```bash
cd src/Api
dotnet build
```

### Testes de Health Check
```bash
# Verificar health checks
curl http://localhost:5000/health
curl http://localhost:5000/health/ready
curl http://localhost:5000/health/live
```

### Verificar Logs
- Verificar se os logs estão sendo gerados em `logs/`
- Testar estrutura de logs no console
- Verificar logs de erro em `logs/errors/`

### Testar Rate Limiting
```bash
# Fazer múltiplas requests para testar rate limit
for i in {1..10}; do curl -i http://localhost:5000/api/test; done
```

## 📋 Próximos Passos

- **Parte 12:** API Endpoints e Docker (Finalização)

---

**Tempo total estimado desta parte:** 45-60 minutos  
**Dificuldade:** ⭐⭐⭐⭐  
**Status:** Configuração de infraestrutura completa ✅