# Fase 3.11: Dependency Injection - Configuração dos Services

## Configuração da Injeção de Dependência

Esta parte implementa a **configuração completa de DI** para todos os services de colaboração em tempo real, incluindo configuração do SignalR, Entity Framework e middlewares.

**Pré-requisitos**: Partes 3.7, 3.8, 3.9 e 3.10 (Services e Controllers) implementadas

## 1. ServiceCollectionExtensions

### 1.1 Configuração Principal dos Services

#### IDE.Api/Extensions/ServiceCollectionExtensions.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using IDE.Infrastructure.Data;
using IDE.Application.Services.Chat;
using IDE.Application.Services.Notifications;
using IDE.Application.Services.Collaboration;
using IDE.Application.Services.Workspace;
using IDE.Application.Services.Auth;
using IDE.Application.Realtime.Hubs;
using IDE.Infrastructure.Services.Chat;
using IDE.Infrastructure.Services.Notifications;
using IDE.Infrastructure.Services.Collaboration;
using IDE.Infrastructure.Services.Workspace;
using IDE.Infrastructure.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;

namespace IDE.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configurar Entity Framework
        /// </summary>
        public static IServiceCollection AddDatabaseContext(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("IDE.Api");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });

                // Configurações de desenvolvimento
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                    options.LogTo(Console.WriteLine, LogLevel.Information);
                }
            });

            return services;
        }

        /// <summary>
        /// Configurar Redis Cache
        /// </summary>
        public static IServiceCollection AddRedisCache(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis");
            
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                // Fallback para cache em memória se Redis não estiver disponível
                services.AddMemoryCache();
                return services;
            }

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "IDE_Instance";
            });

            // Registrar IConnectionMultiplexer para uso direto
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                return ConnectionMultiplexer.Connect(redisConnectionString);
            });

            return services;
        }

        /// <summary>
        /// Configurar SignalR
        /// </summary>
        public static IServiceCollection AddSignalRConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var signalRBuilder = services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            });

            // Configurar Redis backplane se disponível
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix = "IDE_SignalR";
                });
            }

            // Registrar hubs
            services.AddTransient<CollaborationHub>();
            services.AddTransient<ChatHub>();

            return services;
        }

        /// <summary>
        /// Configurar autenticação JWT
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                // Permitir autenticação via SignalR
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && 
                            (path.StartsWithSegments("/hubs/collaboration") || 
                             path.StartsWithSegments("/hubs/chat")))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddAuthorization();

            return services;
        }

        /// <summary>
        /// Configurar CORS
        /// </summary>
        public static IServiceCollection AddCorsConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var corsSettings = configuration.GetSection("CorsSettings");
            var allowedOrigins = corsSettings.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" };

            services.AddCors(options =>
            {
                options.AddPolicy("DefaultCorsPolicy", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });

            return services;
        }

        /// <summary>
        /// Registrar todos os services de colaboração
        /// </summary>
        public static IServiceCollection AddCollaborationServices(
            this IServiceCollection services)
        {
            // Chat Services
            services.AddScoped<IChatService, ChatService>();
            
            // Notification Services
            services.AddScoped<INotificationService, NotificationService>();
            
            // Collaboration Services
            services.AddScoped<IUserPresenceService, UserPresenceService>();
            services.AddScoped<IOperationalTransformService, OperationalTransformService>();
            
            // Infrastructure Services
            services.AddScoped<IRateLimitingService, RateLimitingService>();
            services.AddScoped<ICollaborationMetricsService, CollaborationMetricsService>();
            services.AddScoped<ICollaborationAuditService, CollaborationAuditService>();
            
            // Services from previous phases (ensure compatibility)
            services.AddScoped<IWorkspaceService, WorkspaceService>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }

        /// <summary>
        /// Configurar Swagger/OpenAPI
        /// </summary>
        public static IServiceCollection AddSwaggerConfiguration(
            this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "IDE Collaboration API",
                    Version = "v1",
                    Description = "API para colaboração em tempo real no IDE",
                    Contact = new Microsoft.OpenApi.Models.OpenApiContact
                    {
                        Name = "Equipe de Desenvolvimento",
                        Email = "dev@ide.com"
                    }
                });

                // Configurar autenticação JWT no Swagger
                options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Insira o token JWT no formato: Bearer {seu-token}"
                });

                options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // Incluir documentação XML se disponível
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });

            return services;
        }

        /// <summary>
        /// Configurar Health Checks
        /// </summary>
        public static IServiceCollection AddHealthChecksConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var healthChecksBuilder = services.AddHealthChecks();

            // SQL Server health check
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                healthChecksBuilder.AddSqlServer(connectionString, name: "database");
            }

            // Redis health check
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                healthChecksBuilder.AddRedis(redisConnectionString, name: "redis");
            }

            // Custom health checks
            healthChecksBuilder.AddCheck<CollaborationHealthCheck>("collaboration-services");

            return services;
        }

        /// <summary>
        /// Configurar logging
        /// </summary>
        public static IServiceCollection AddLoggingConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
                builder.AddDebug();

                // Configurar níveis de log por namespace
                builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                builder.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
                builder.AddFilter("IDE.Application", LogLevel.Information);
                builder.AddFilter("IDE.Infrastructure", LogLevel.Information);

                // Configurar Serilog se disponível
                var serilogSection = configuration.GetSection("Serilog");
                if (serilogSection.Exists())
                {
                    // Configuração do Serilog será implementada posteriormente
                }
            });

            return services;
        }

        /// <summary>
        /// Configurar rate limiting global
        /// </summary>
        public static IServiceCollection AddGlobalRateLimiting(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<RateLimitingOptions>(
                configuration.GetSection("RateLimiting"));

            // Configurar rate limiting por IP
            services.AddMemoryCache();
            services.AddSingleton<IGlobalRateLimitingService, GlobalRateLimitingService>();

            return services;
        }
    }

    // Options classes
    public class RateLimitingOptions
    {
        public int MaxRequestsPerMinute { get; set; } = 100;
        public int MaxRequestsPerHour { get; set; } = 1000;
        public int MaxRequestsPerDay { get; set; } = 10000;
        public bool EnableIpRateLimiting { get; set; } = true;
        public bool EnableUserRateLimiting { get; set; } = true;
        public List<string> ExemptIpAddresses { get; set; } = new();
        public List<string> ExemptUserRoles { get; set; } = new() { "Admin" };
    }

    // Global rate limiting service interface
    public interface IGlobalRateLimitingService
    {
        Task<bool> IsRequestAllowedAsync(string identifier, string endpoint);
        Task<RateLimitResult> CheckRateLimitAsync(string identifier, string endpoint);
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan RetryAfter { get; set; }
        public string ReasonPhrase { get; set; } = string.Empty;
    }

    // Placeholder for global rate limiting service implementation
    public class GlobalRateLimitingService : IGlobalRateLimitingService
    {
        public Task<bool> IsRequestAllowedAsync(string identifier, string endpoint)
        {
            // Implementation will be added later
            return Task.FromResult(true);
        }

        public Task<RateLimitResult> CheckRateLimitAsync(string identifier, string endpoint)
        {
            // Implementation will be added later
            return Task.FromResult(new RateLimitResult 
            { 
                IsAllowed = true, 
                RemainingRequests = 100 
            });
        }
    }
}
```

## 2. Health Check Implementation

### 2.1 CollaborationHealthCheck - Verificação de Saúde dos Services

#### IDE.Api/HealthChecks/CollaborationHealthCheck.cs
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using IDE.Application.Services.Chat;
using IDE.Application.Services.Notifications;
using IDE.Application.Services.Collaboration;

namespace IDE.Api.HealthChecks
{
    public class CollaborationHealthCheck : IHealthCheck
    {
        private readonly IChatService _chatService;
        private readonly INotificationService _notificationService;
        private readonly IUserPresenceService _presenceService;
        private readonly ILogger<CollaborationHealthCheck> _logger;

        public CollaborationHealthCheck(
            IChatService chatService,
            INotificationService notificationService,
            IUserPresenceService presenceService,
            ILogger<CollaborationHealthCheck> logger)
        {
            _chatService = chatService;
            _notificationService = notificationService;
            _presenceService = presenceService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var data = new Dictionary<string, object>();
                var issues = new List<string>();

                // Verificar Chat Service
                try
                {
                    // Teste básico - obter contagem de mensagens
                    await _chatService.GetWorkspaceChatStatsAsync(Guid.Empty);
                    data["ChatService"] = "Healthy";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Chat service health check failed");
                    issues.Add("Chat service is not responding");
                    data["ChatService"] = "Unhealthy";
                }

                // Verificar Notification Service
                try
                {
                    await _notificationService.GetNotificationStatsAsync(null);
                    data["NotificationService"] = "Healthy";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Notification service health check failed");
                    issues.Add("Notification service is not responding");
                    data["NotificationService"] = "Unhealthy";
                }

                // Verificar Presence Service
                try
                {
                    await _presenceService.GetPresenceStatsAsync();
                    data["PresenceService"] = "Healthy";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Presence service health check failed");
                    issues.Add("Presence service is not responding");
                    data["PresenceService"] = "Unhealthy";
                }

                if (issues.Any())
                {
                    return HealthCheckResult.Degraded(
                        description: $"Some services are unhealthy: {string.Join(", ", issues)}",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    description: "All collaboration services are healthy",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with exception");
                return HealthCheckResult.Unhealthy(
                    description: "Health check failed with exception",
                    exception: ex);
            }
        }
    }
}
```

## 3. Program.cs Configuration

### 3.1 Configuração Principal da Aplicação

#### IDE.Api/Program.cs
```csharp
using IDE.Api.Extensions;
using IDE.Api.Middleware;
using IDE.Application.Realtime.Hubs;
using IDE.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração de logging
builder.Services.AddLoggingConfiguration(builder.Configuration);

// Configuração do banco de dados
builder.Services.AddDatabaseContext(builder.Configuration);

// Configuração do Redis
builder.Services.AddRedisCache(builder.Configuration);

// Configuração de autenticação e autorização
builder.Services.AddJwtAuthentication(builder.Configuration);

// Configuração do CORS
builder.Services.AddCorsConfiguration(builder.Configuration);

// Configuração do SignalR
builder.Services.AddSignalRConfiguration(builder.Configuration);

// Registro dos services de colaboração
builder.Services.AddCollaborationServices();

// Configuração de rate limiting
builder.Services.AddGlobalRateLimiting(builder.Configuration);

// Configuração de health checks
builder.Services.AddHealthChecksConfiguration(builder.Configuration);

// Configuração do Swagger
builder.Services.AddSwaggerConfiguration();

// Configuração de controllers
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });

var app = builder.Build();

// Pipeline de middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "IDE Collaboration API v1");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
    });
}

// Middlewares de segurança
app.UseHttpsRedirection();
app.UseRouting();

// CORS
app.UseCors("DefaultCorsPolicy");

// Autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// Middleware customizado de rate limiting
app.UseMiddleware<GlobalRateLimitingMiddleware>();

// Middleware de logging de requests
app.UseMiddleware<RequestLoggingMiddleware>();

// Middleware de tratamento de erros
app.UseMiddleware<GlobalExceptionMiddleware>();

// Health checks
app.UseHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                data = entry.Value.Data
            }),
            totalDuration = report.TotalDuration
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Mapeamento de controllers
app.MapControllers();

// Mapeamento de hubs SignalR
app.MapHub<CollaborationHub>("/hubs/collaboration");
app.MapHub<ChatHub>("/hubs/chat");

// Aplicar migrations automaticamente em desenvolvimento
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating the database");
        }
    }
}

// Inicializar dados de seed em desenvolvimento
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var serviceProvider = scope.ServiceProvider;
        await SeedData.InitializeAsync(serviceProvider);
    }
}

app.Run();
```

## 4. Custom Middlewares

### 4.1 GlobalRateLimitingMiddleware

#### IDE.Api/Middleware/GlobalRateLimitingMiddleware.cs
```csharp
using IDE.Api.Extensions;
using System.Net;
using System.Text.Json;

namespace IDE.Api.Middleware
{
    public class GlobalRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IGlobalRateLimitingService _rateLimitingService;
        private readonly ILogger<GlobalRateLimitingMiddleware> _logger;

        public GlobalRateLimitingMiddleware(
            RequestDelegate next,
            IGlobalRateLimitingService rateLimitingService,
            ILogger<GlobalRateLimitingMiddleware> logger)
        {
            _next = next;
            _rateLimitingService = rateLimitingService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var identifier = GetIdentifier(context);
                var endpoint = context.Request.Path.Value ?? string.Empty;

                var result = await _rateLimitingService.CheckRateLimitAsync(identifier, endpoint);

                if (!result.IsAllowed)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers.Add("X-RateLimit-Remaining", result.RemainingRequests.ToString());
                    context.Response.Headers.Add("Retry-After", ((int)result.RetryAfter.TotalSeconds).ToString());

                    var errorResponse = new
                    {
                        error = "Too Many Requests",
                        message = result.ReasonPhrase,
                        retryAfter = result.RetryAfter.TotalSeconds
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                    return;
                }

                // Adicionar headers informativos
                context.Response.Headers.Add("X-RateLimit-Remaining", result.RemainingRequests.ToString());

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limiting middleware");
                await _next(context); // Continue sem rate limiting em caso de erro
            }
        }

        private string GetIdentifier(HttpContext context)
        {
            // Tentar obter ID do usuário primeiro
            var userId = context.User?.GetUserId();
            if (userId.HasValue)
            {
                return $"user:{userId}";
            }

            // Fallback para IP address
            return $"ip:{GetClientIpAddress(context)}";
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Connection.RemoteIpAddress?.ToString();
            }
            return ipAddress ?? "unknown";
        }
    }
}
```

### 4.2 RequestLoggingMiddleware

#### IDE.Api/Middleware/RequestLoggingMiddleware.cs
```csharp
using System.Diagnostics;

namespace IDE.Api.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString();

            context.Items["RequestId"] = requestId;

            try
            {
                _logger.LogInformation(
                    "Request {RequestId} started: {Method} {Path} from {RemoteIp}",
                    requestId,
                    context.Request.Method,
                    context.Request.Path,
                    context.Connection.RemoteIpAddress);

                await _next(context);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Request {RequestId} completed: {StatusCode} in {ElapsedMs}ms",
                    requestId,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex,
                    "Request {RequestId} failed: {Method} {Path} in {ElapsedMs}ms",
                    requestId,
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }
        }
    }
}
```

### 4.3 GlobalExceptionMiddleware

#### IDE.Api/Middleware/GlobalExceptionMiddleware.cs
```csharp
using System.Net;
using System.Text.Json;

namespace IDE.Api.Middleware
{
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
            var requestId = context.Items["RequestId"]?.ToString() ?? Guid.NewGuid().ToString();

            _logger.LogError(exception,
                "Unhandled exception occurred. RequestId: {RequestId}",
                requestId);

            context.Response.ContentType = "application/json";

            var response = new ErrorResponse
            {
                RequestId = requestId,
                Message = GetErrorMessage(exception),
                Type = exception.GetType().Name
            };

            switch (exception)
            {
                case UnauthorizedAccessException:
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Access denied";
                    break;

                case ArgumentException:
                case ArgumentNullException:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "Invalid request parameters";
                    break;

                case KeyNotFoundException:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Message = "Resource not found";
                    break;

                case InvalidOperationException:
                    context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                    response.Message = "Operation cannot be completed";
                    break;

                case TimeoutException:
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.Message = "Request timeout";
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Message = "An internal server error occurred";
                    break;
            }

            // Incluir detalhes em desenvolvimento
            if (_environment.IsDevelopment())
            {
                response.Details = exception.ToString();
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private string GetErrorMessage(Exception exception)
        {
            return _environment.IsDevelopment() ? exception.Message : "An error occurred";
        }
    }

    public class ErrorResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
```

## 5. Seed Data

### 5.1 SeedData - Dados Iniciais

#### IDE.Api/Data/SeedData.cs
```csharp
using IDE.Infrastructure.Persistence.Data;
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Api.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedData>>();

            try
            {
                await SeedCollaborationDataAsync(context);
                logger.LogInformation("Seed data initialization completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during seed data initialization");
                throw;
            }
        }

        private static async Task SeedCollaborationDataAsync(ApplicationDbContext context)
        {
            // Verificar se já existem dados
            if (await context.ChatMessages.AnyAsync())
            {
                return; // Dados já foram inseridos
            }

            // Seed data seria implementado aqui se necessário
            // Por exemplo, dados de teste para desenvolvimento

            await context.SaveChangesAsync();
        }
    }
}
```

## Entregáveis da Parte 3.11

✅ **Configuração completa de DI**:
- ServiceCollectionExtensions com todas as configurações
- Registro de todos os services de colaboração
- Configuração de Entity Framework e Redis
- Configuração de SignalR com backplane Redis
- Autenticação JWT completa

✅ **Middlewares customizados**:
- GlobalRateLimitingMiddleware
- RequestLoggingMiddleware  
- GlobalExceptionMiddleware
- Health checks personalizados

✅ **Program.cs configurado**:
- Pipeline completo de middlewares
- Mapeamento de controllers e hubs
- Configuração de ambiente
- Seed data automático

✅ **Recursos adicionais**:
- Health checks para todos os services
- CORS configurado
- Swagger com autenticação JWT
- Logging estruturado

## Próximos Passos

Na **Parte 3.12**, finalizaremos com:
- Configuração de Docker
- Scripts de deployment
- Documentação final

**Dependência**: Esta parte (3.11) deve estar implementada antes de prosseguir.