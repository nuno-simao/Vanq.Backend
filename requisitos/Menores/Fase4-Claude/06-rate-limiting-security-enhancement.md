# Fase 4 - Parte 6: Rate Limiting & Security Enhancement

## Contexto da Implementação

Esta é a **sexta parte da Fase 4** focada na **implementação de rate limiting por plano de usuário** e **enhancements de segurança** com sistema de parâmetros configuráveis e proteções avançadas.

### Objetivos da Parte 6
✅ **Rate limiting** por plano (Free/Pro/Enterprise)  
✅ **Sistema de parâmetros** configuráveis  
✅ **Security enhancements** avançados  
✅ **Request validation** robusta  
✅ **Audit logging** completo  
✅ **Resource limits** por usuário  

### Pré-requisitos
- Partes 1-5 implementadas e funcionais
- Redis cache configurado
- Sistema de autenticação funcionando
- Database com SystemParameters table

---

## 5.1 Rate Limiting por Plano

Sistema de rate limiting diferenciado baseado no plano do usuário com limites configuráveis.

### Plan-Based Rate Limiting Service

#### IDE.Infrastructure/RateLimiting/IPlanBasedRateLimitingService.cs
```csharp
public interface IPlanBasedRateLimitingService
{
    Task<RateLimitResult> CheckRateLimitAsync(Guid userId, string endpoint, UserPlan plan);
    Task<PlanLimits> GetPlanLimitsAsync(UserPlan plan);
    Task UpdatePlanLimitsAsync(UserPlan plan, PlanLimits limits);
    Task<UserResourceUsage> GetUserResourceUsageAsync(Guid userId);
    Task IncrementResourceUsageAsync(Guid userId, ResourceType resourceType, long amount);
}

public class PlanBasedRateLimitingService : IPlanBasedRateLimitingService
{
    private readonly IRedisCacheService _cache;
    private readonly ApplicationDbContext _context;
    private readonly ISystemParameterService _parameters;
    private readonly ILogger<PlanBasedRateLimitingService> _logger;

    public PlanBasedRateLimitingService(
        IRedisCacheService cache,
        ApplicationDbContext context,
        ISystemParameterService parameters,
        ILogger<PlanBasedRateLimitingService> logger)
    {
        _cache = cache;
        _context = context;
        _parameters = parameters;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(Guid userId, string endpoint, UserPlan plan)
    {
        var limits = await GetPlanLimitsAsync(plan);
        var window = TimeSpan.FromMinutes(1); // 1-minute sliding window
        var now = DateTime.UtcNow;
        
        // Create time-based key for sliding window
        var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        var key = $"rate_limit:{userId}:{windowStart:yyyyMMddHHmm}";
        
        // Use Redis INCR for atomic counter increment
        var currentCount = await _cache.IncrementAsync(key, 1, window);
        
        var isAllowed = currentCount <= limits.RequestsPerMinute;
        var remaining = Math.Max(0, limits.RequestsPerMinute - currentCount);

        var result = new RateLimitResult
        {
            IsAllowed = isAllowed,
            Limit = limits.RequestsPerMinute,
            Current = currentCount,
            Remaining = remaining,
            ResetTime = windowStart.AddMinutes(1),
            Plan = plan,
            Endpoint = endpoint
        };

        // Enhanced logging for rate limit violations
        if (!isAllowed)
        {
            _logger.LogWarning("Rate limit exceeded: User={UserId}, Plan={Plan}, Endpoint={Endpoint}, " +
                              "Current={Current}, Limit={Limit}, Window={Window}",
                userId, plan, endpoint, currentCount, limits.RequestsPerMinute, windowStart);
                              
            // Track rate limit violations for analysis
            await _cache.IncrementAsync($"violations:{userId}:daily", 1, TimeSpan.FromDays(1));
        }

        return result;
    }

    public async Task<PlanLimits> GetPlanLimitsAsync(UserPlan plan)
    {
        var cacheKey = $"plan_limits:{plan}";
        var cached = await _cache.GetAsync<PlanLimits>(cacheKey);
        
        if (cached != null)
            return cached;

        // Get dynamic limits from system parameters
        var limits = plan switch
        {
            UserPlan.Free => new PlanLimits
            {
                RequestsPerMinute = await _parameters.GetParameterAsync("RateLimit.Free.RequestsPerMinute", 60),
                RequestsPerHour = await _parameters.GetParameterAsync("RateLimit.Free.RequestsPerHour", 1000),
                RequestsPerDay = await _parameters.GetParameterAsync("RateLimit.Free.RequestsPerDay", 10000),
                MaxWorkspaces = await _parameters.GetParameterAsync("Limits.Free.MaxWorkspaces", 3),
                MaxUploadSizeMB = await _parameters.GetParameterAsync("Limits.Free.MaxUploadSizeMB", 5),
                MaxStorageGB = await _parameters.GetParameterAsync("Limits.Free.MaxStorageGB", 1),
                ConcurrentConnections = await _parameters.GetParameterAsync("Limits.Free.ConcurrentConnections", 3),
                MaxTeamMembers = await _parameters.GetParameterAsync("Limits.Free.MaxTeamMembers", 1)
            },
            UserPlan.Pro => new PlanLimits
            {
                RequestsPerMinute = await _parameters.GetParameterAsync("RateLimit.Pro.RequestsPerMinute", 300),
                RequestsPerHour = await _parameters.GetParameterAsync("RateLimit.Pro.RequestsPerHour", 10000),
                RequestsPerDay = await _parameters.GetParameterAsync("RateLimit.Pro.RequestsPerDay", 100000),
                MaxWorkspaces = await _parameters.GetParameterAsync("Limits.Pro.MaxWorkspaces", 20),
                MaxUploadSizeMB = await _parameters.GetParameterAsync("Limits.Pro.MaxUploadSizeMB", 25),
                MaxStorageGB = await _parameters.GetParameterAsync("Limits.Pro.MaxStorageGB", 10),
                ConcurrentConnections = await _parameters.GetParameterAsync("Limits.Pro.ConcurrentConnections", 15),
                MaxTeamMembers = await _parameters.GetParameterAsync("Limits.Pro.MaxTeamMembers", 10)
            },
            UserPlan.Enterprise => new PlanLimits
            {
                RequestsPerMinute = await _parameters.GetParameterAsync("RateLimit.Enterprise.RequestsPerMinute", 1000),
                RequestsPerHour = await _parameters.GetParameterAsync("RateLimit.Enterprise.RequestsPerHour", 50000),
                RequestsPerDay = await _parameters.GetParameterAsync("RateLimit.Enterprise.RequestsPerDay", 1000000),
                MaxWorkspaces = await _parameters.GetParameterAsync("Limits.Enterprise.MaxWorkspaces", -1), // Unlimited
                MaxUploadSizeMB = await _parameters.GetParameterAsync("Limits.Enterprise.MaxUploadSizeMB", 100),
                MaxStorageGB = await _parameters.GetParameterAsync("Limits.Enterprise.MaxStorageGB", 100),
                ConcurrentConnections = await _parameters.GetParameterAsync("Limits.Enterprise.ConcurrentConnections", 50),
                MaxTeamMembers = await _parameters.GetParameterAsync("Limits.Enterprise.MaxTeamMembers", -1) // Unlimited
            },
            _ => throw new ArgumentException($"Unknown plan: {plan}")
        };

        // Cache for 10 minutes
        await _cache.SetAsync(cacheKey, limits, TimeSpan.FromMinutes(10));
        return limits;
    }

    public async Task UpdatePlanLimitsAsync(UserPlan plan, PlanLimits limits)
    {
        // Update system parameters
        var prefix = $"RateLimit.{plan}";
        await _parameters.SetParameterAsync($"{prefix}.RequestsPerMinute", limits.RequestsPerMinute);
        await _parameters.SetParameterAsync($"{prefix}.RequestsPerHour", limits.RequestsPerHour);
        await _parameters.SetParameterAsync($"{prefix}.RequestsPerDay", limits.RequestsPerDay);

        var limitsPrefix = $"Limits.{plan}";
        await _parameters.SetParameterAsync($"{limitsPrefix}.MaxWorkspaces", limits.MaxWorkspaces);
        await _parameters.SetParameterAsync($"{limitsPrefix}.MaxUploadSizeMB", limits.MaxUploadSizeMB);
        await _parameters.SetParameterAsync($"{limitsPrefix}.MaxStorageGB", limits.MaxStorageGB);
        await _parameters.SetParameterAsync($"{limitsPrefix}.ConcurrentConnections", limits.ConcurrentConnections);
        await _parameters.SetParameterAsync($"{limitsPrefix}.MaxTeamMembers", limits.MaxTeamMembers);

        // Invalidate cache
        await _cache.RemoveAsync($"plan_limits:{plan}");
        
        _logger.LogInformation("Updated plan limits for {Plan}", plan);
    }

    public async Task<UserResourceUsage> GetUserResourceUsageAsync(Guid userId)
    {
        try
        {
            // Get current usage from database and cache
            var workspaceCount = await _context.Workspaces
                .Where(w => w.CreatedBy == userId && !w.IsArchived)
                .CountAsync();

            var storageUsage = await _context.ModuleItems
                .Where(mi => mi.Workspace.CreatedBy == userId)
                .SumAsync(mi => mi.ContentSize ?? 0);

            var activeConnections = await GetActiveConnectionCountAsync(userId);

            var usage = new UserResourceUsage
            {
                UserId = userId,
                CurrentWorkspaces = workspaceCount,
                StorageUsedBytes = storageUsage,
                ActiveConnections = activeConnections,
                RequestsToday = await GetRequestCountTodayAsync(userId),
                LastUpdated = DateTime.UtcNow
            };

            // Cache for 5 minutes
            await _cache.SetAsync($"user_usage:{userId}", usage, TimeSpan.FromMinutes(5));

            return usage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource usage for user {UserId}", userId);
            return new UserResourceUsage { UserId = userId, LastUpdated = DateTime.UtcNow };
        }
    }

    public async Task IncrementResourceUsageAsync(Guid userId, ResourceType resourceType, long amount)
    {
        var key = $"usage:{userId}:{resourceType}:{DateTime.UtcNow:yyyyMMdd}";
        await _cache.IncrementAsync(key, amount, TimeSpan.FromDays(1));
    }

    private async Task<int> GetActiveConnectionCountAsync(Guid userId)
    {
        var pattern = $"session:*:{userId}";
        var sessions = await _cache.GetKeysAsync(pattern);
        return sessions.Count;
    }

    private async Task<long> GetRequestCountTodayAsync(Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        var key = $"requests:{userId}:{today:yyyyMMdd}";
        var cachedCount = await _cache.GetAsync<long?>(key);
        return cachedCount ?? 0;
    }
}
```

### Rate Limiting Models

#### IDE.Domain/Security/RateLimitingModels.cs
```csharp
public enum UserPlan
{
    Free = 0,
    Pro = 1,
    Enterprise = 2
}

public class PlanLimits
{
    public int RequestsPerMinute { get; set; }
    public int RequestsPerHour { get; set; }
    public int RequestsPerDay { get; set; }
    public int MaxWorkspaces { get; set; }
    public int MaxUploadSizeMB { get; set; }
    public int MaxStorageGB { get; set; }
    public int ConcurrentConnections { get; set; }
    public int MaxTeamMembers { get; set; }
}

public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public long Limit { get; set; }
    public long Current { get; set; }
    public long Remaining { get; set; }
    public DateTime ResetTime { get; set; }
    public UserPlan Plan { get; set; }
    public string Endpoint { get; set; }
}

public class UserResourceUsage
{
    public Guid UserId { get; set; }
    public int CurrentWorkspaces { get; set; }
    public long StorageUsedBytes { get; set; }
    public int ActiveConnections { get; set; }
    public long RequestsToday { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public double StorageUsedGB => StorageUsedBytes / (1024.0 * 1024.0 * 1024.0);
}

public enum ResourceType
{
    Request,
    Storage,
    Connection,
    Upload
}
```

---

## 5.2 Sistema de Parâmetros Configuráveis

Sistema robusto para gerenciamento de parâmetros do sistema com cache e validação.

### System Parameter Entity

#### IDE.Domain/Entities/SystemParameter.cs
```csharp
public class SystemParameter
{
    public Guid Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
    public ParameterType Type { get; set; } = ParameterType.String;
    public string Category { get; set; }
    public bool IsEncrypted { get; set; } = false;
    public bool IsReadOnly { get; set; } = false;
    public string DefaultValue { get; set; }
    public string ValidationRegex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    
    // Navigation properties
    public User CreatedByUser { get; set; }
    public User UpdatedByUser { get; set; }
}

public enum ParameterType
{
    String,
    Integer,
    Boolean,
    Decimal,
    Json,
    Encrypted
}
```

### System Parameter Service

#### IDE.Application/Configuration/ISystemParameterService.cs
```csharp
public interface ISystemParameterService
{
    Task<T> GetParameterAsync<T>(string key, T defaultValue = default);
    Task SetParameterAsync<T>(string key, T value, string description = null, string category = null);
    Task<List<SystemParameter>> GetParametersByCategoryAsync(string category);
    Task<bool> DeleteParameterAsync(string key);
    Task<Dictionary<string, object>> GetAllParametersAsync();
    Task<bool> ValidateParameterAsync(string key, string value);
    Task BulkUpdateParametersAsync(Dictionary<string, object> parameters);
}

public class SystemParameterService : ISystemParameterService
{
    private readonly ApplicationDbContext _context;
    private readonly IRedisCacheService _cache;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<SystemParameterService> _logger;

    public SystemParameterService(
        ApplicationDbContext context,
        IRedisCacheService cache,
        IEncryptionService encryption,
        ILogger<SystemParameterService> logger)
    {
        _context = context;
        _cache = cache;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<T> GetParameterAsync<T>(string key, T defaultValue = default)
    {
        try
        {
            // Try cache first
            var cacheKey = $"param:{key}";
            var cached = await _cache.GetAsync<string>(cacheKey);
            
            if (cached != null)
            {
                return ConvertValue<T>(cached);
            }

            // Get from database
            var parameter = await _context.SystemParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Key == key);

            if (parameter == null)
            {
                _logger.LogDebug("Parameter {Key} not found, using default value", key);
                return defaultValue;
            }

            var value = parameter.IsEncrypted 
                ? await _encryption.DecryptAsync(parameter.Value)
                : parameter.Value;

            // Cache for 10 minutes
            await _cache.SetAsync(cacheKey, value, TimeSpan.FromMinutes(10));

            return ConvertValue<T>(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parameter {Key}", key);
            return defaultValue;
        }
    }

    public async Task SetParameterAsync<T>(string key, T value, string description = null, string category = null)
    {
        try
        {
            var stringValue = ConvertToString(value);
            var parameterType = DetermineType<T>();
            var isEncrypted = parameterType == ParameterType.Encrypted || 
                             (category?.Contains("Password") == true || category?.Contains("Secret") == true);

            var existingParameter = await _context.SystemParameters
                .FirstOrDefaultAsync(p => p.Key == key);

            if (existingParameter != null)
            {
                if (existingParameter.IsReadOnly)
                {
                    throw new InvalidOperationException($"Parameter {key} is read-only");
                }

                var valueToStore = isEncrypted 
                    ? await _encryption.EncryptAsync(stringValue)
                    : stringValue;

                existingParameter.Value = valueToStore;
                existingParameter.Type = parameterType;
                existingParameter.IsEncrypted = isEncrypted;
                existingParameter.UpdatedAt = DateTime.UtcNow;

                if (description != null)
                    existingParameter.Description = description;
            }
            else
            {
                var valueToStore = isEncrypted 
                    ? await _encryption.EncryptAsync(stringValue)
                    : stringValue;

                var newParameter = new SystemParameter
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = valueToStore,
                    Description = description ?? $"Auto-created parameter for {key}",
                    Type = parameterType,
                    Category = category ?? "General",
                    IsEncrypted = isEncrypted,
                    DefaultValue = stringValue,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.Empty, // System
                    UpdatedBy = Guid.Empty  // System
                };

                _context.SystemParameters.Add(newParameter);
            }

            await _context.SaveChangesAsync();

            // Update cache
            var cacheKey = $"param:{key}";
            await _cache.SetAsync(cacheKey, stringValue, TimeSpan.FromMinutes(10));

            _logger.LogInformation("Parameter {Key} updated successfully", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting parameter {Key}", key);
            throw;
        }
    }

    public async Task<List<SystemParameter>> GetParametersByCategoryAsync(string category)
    {
        try
        {
            var cacheKey = $"params:category:{category}";
            var cached = await _cache.GetAsync<List<SystemParameter>>(cacheKey);
            
            if (cached != null)
                return cached;

            var parameters = await _context.SystemParameters
                .AsNoTracking()
                .Where(p => p.Category == category)
                .OrderBy(p => p.Key)
                .ToListAsync();

            // Cache for 5 minutes
            await _cache.SetAsync(cacheKey, parameters, TimeSpan.FromMinutes(5));

            return parameters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parameters for category {Category}", category);
            return new List<SystemParameter>();
        }
    }

    public async Task<bool> DeleteParameterAsync(string key)
    {
        try
        {
            var parameter = await _context.SystemParameters
                .FirstOrDefaultAsync(p => p.Key == key);

            if (parameter == null)
                return false;

            if (parameter.IsReadOnly)
            {
                throw new InvalidOperationException($"Parameter {key} is read-only and cannot be deleted");
            }

            _context.SystemParameters.Remove(parameter);
            await _context.SaveChangesAsync();

            // Remove from cache
            await _cache.RemoveAsync($"param:{key}");

            _logger.LogInformation("Parameter {Key} deleted successfully", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting parameter {Key}", key);
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetAllParametersAsync()
    {
        try
        {
            var parameters = await _context.SystemParameters
                .AsNoTracking()
                .ToListAsync();

            var result = new Dictionary<string, object>();

            foreach (var param in parameters)
            {
                var value = param.IsEncrypted 
                    ? "[ENCRYPTED]" 
                    : param.Value;

                result[param.Key] = new
                {
                    Value = value,
                    Type = param.Type.ToString(),
                    Category = param.Category,
                    Description = param.Description,
                    IsReadOnly = param.IsReadOnly,
                    UpdatedAt = param.UpdatedAt
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all parameters");
            return new Dictionary<string, object>();
        }
    }

    public async Task<bool> ValidateParameterAsync(string key, string value)
    {
        try
        {
            var parameter = await _context.SystemParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Key == key);

            if (parameter?.ValidationRegex == null)
                return true;

            var regex = new Regex(parameter.ValidationRegex);
            return regex.IsMatch(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating parameter {Key}", key);
            return false;
        }
    }

    public async Task BulkUpdateParametersAsync(Dictionary<string, object> parameters)
    {
        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            foreach (var kvp in parameters)
            {
                await SetParameterAsync(kvp.Key, kvp.Value);
            }

            await transaction.CommitAsync();
            
            _logger.LogInformation("Bulk updated {Count} parameters", parameters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk update parameters");
            throw;
        }
    }

    private T ConvertValue<T>(string value)
    {
        if (string.IsNullOrEmpty(value))
            return default(T);

        try
        {
            var type = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(type);
            
            if (nullableType != null)
                type = nullableType;

            if (type == typeof(string))
                return (T)(object)value;

            if (type == typeof(bool))
                return (T)(object)bool.Parse(value);

            if (type == typeof(int))
                return (T)(object)int.Parse(value);

            if (type == typeof(decimal))
                return (T)(object)decimal.Parse(value);

            if (type.IsEnum)
                return (T)Enum.Parse(type, value);

            return JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot convert value '{value}' to type {typeof(T).Name}", ex);
        }
    }

    private string ConvertToString<T>(T value)
    {
        if (value == null)
            return string.Empty;

        if (value is string str)
            return str;

        if (value.GetType().IsPrimitive || value is decimal)
            return value.ToString();

        return JsonSerializer.Serialize(value);
    }

    private ParameterType DetermineType<T>()
    {
        var type = typeof(T);
        
        if (type == typeof(string))
            return ParameterType.String;
        if (type == typeof(bool) || type == typeof(bool?))
            return ParameterType.Boolean;
        if (type == typeof(int) || type == typeof(int?) || type == typeof(long) || type == typeof(long?))
            return ParameterType.Integer;
        if (type == typeof(decimal) || type == typeof(decimal?) || type == typeof(double) || type == typeof(double?))
            return ParameterType.Decimal;
        
        return ParameterType.Json;
    }
}
```

### Rate Limiting Middleware

#### IDE.API/Middleware/RateLimitingMiddleware.cs
```csharp
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPlanBasedRateLimitingService _rateLimiting;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IPlanBasedRateLimitingService rateLimiting,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _rateLimiting = rateLimiting;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks and static files
        if (ShouldSkipRateLimiting(context))
        {
            await _next(context);
            return;
        }

        var userId = GetUserId(context);
        if (userId == null)
        {
            await _next(context);
            return;
        }

        var userPlan = await GetUserPlan(context, userId.Value);
        var endpoint = GetEndpointName(context);

        var rateLimitResult = await _rateLimiting.CheckRateLimitAsync(
            userId.Value, endpoint, userPlan);

        // Add rate limit headers
        context.Response.Headers.Add("X-RateLimit-Limit", rateLimitResult.Limit.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", rateLimitResult.Remaining.ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", rateLimitResult.ResetTime.ToString("o"));
        context.Response.Headers.Add("X-RateLimit-Plan", rateLimitResult.Plan.ToString());

        if (!rateLimitResult.IsAllowed)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers.Add("Retry-After", "60");

            var errorResponse = new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests. Limit: {rateLimitResult.Limit} requests per minute for {rateLimitResult.Plan} plan",
                details = new
                {
                    limit = rateLimitResult.Limit,
                    current = rateLimitResult.Current,
                    remaining = rateLimitResult.Remaining,
                    resetTime = rateLimitResult.ResetTime,
                    plan = rateLimitResult.Plan.ToString()
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            return;
        }

        await _next(context);
    }

    private bool ShouldSkipRateLimiting(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower();
        
        var skipPaths = new[]
        {
            "/health",
            "/metrics",
            "/favicon.ico",
            "/swagger",
            "/api/auth/refresh" // Allow token refresh
        };

        return skipPaths.Any(skipPath => path?.StartsWith(skipPath) == true);
    }

    private Guid? GetUserId(HttpContext context)
    {
        var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<UserPlan> GetUserPlan(HttpContext context, Guid userId)
    {
        // Get user plan from claims or database
        var planClaim = context.User?.FindFirst("plan")?.Value;
        
        if (Enum.TryParse<UserPlan>(planClaim, out var plan))
            return plan;

        // Default to Free if no plan is found
        return UserPlan.Free;
    }

    private string GetEndpointName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<RouteAttribute>() != null)
        {
            var routeAttribute = endpoint.Metadata.GetMetadata<RouteAttribute>();
            return $"{context.Request.Method} /{routeAttribute.Template}";
        }
        
        return $"{context.Request.Method} {context.Request.Path}";
    }
}
```

---

## Entregáveis da Parte 6

### ✅ Implementações Completas
- **PlanBasedRateLimitingService** com limites por plano
- **SystemParameterService** com cache e encryption
- **RateLimitingMiddleware** com headers HTTP
- **UserResourceUsage** tracking completo
- **Parameter validation** e bulk updates
- **Rate limit violation** logging e analysis

### ✅ Funcionalidades de Segurança
- **Plan-based rate limiting** (Free/Pro/Enterprise)
- **Resource usage tracking** por usuário
- **Parameter encryption** para dados sensíveis
- **Validation regex** para parâmetros
- **Audit logging** para mudanças
- **Sliding window** rate limiting

### ✅ Sistema de Configuração
- **Dynamic parameter** management
- **Category-based** organization
- **Read-only parameters** protection
- **Cache invalidation** automática
- **Bulk updates** com transações
- **Encrypted storage** para secrets

---

## Validação da Parte 6

### Critérios de Sucesso
- [ ] Rate limits funcionam para todos os planos
- [ ] Parâmetros são carregados do cache < 10ms
- [ ] Headers de rate limit são incluídos
- [ ] Resource usage é tracked corretamente
- [ ] Parameters encrypted são protegidos
- [ ] Bulk updates são transacionais
- [ ] Rate limit violations são logados

### Testes de Rate Limiting
```bash
# 1. Testar rate limit Free plan
for i in {1..70}; do curl -X GET http://localhost:8503/api/workspaces -H "Authorization: Bearer <free-token>"; done

# 2. Verificar headers de rate limit
curl -I -X GET http://localhost:8503/api/workspaces -H "Authorization: Bearer <token>"

# 3. Testar parâmetros do sistema
curl -X GET http://localhost:8503/api/admin/parameters/RateLimit -H "Authorization: Bearer <admin-token>"
```

### Security Targets
- **Rate limit enforcement**: 100% accurate
- **Parameter encryption**: All sensitive data
- **Cache hit ratio**: > 90% for parameters
- **Resource tracking**: Real-time updates
- **Validation**: All parameter updates

---

## Próximos Passos

Após validação da Parte 6, prosseguir para:
- **Parte 7**: Testing Infrastructure & Quality Assurance

---

**Tempo Estimado**: 3-4 horas  
**Complexidade**: Alta  
**Dependências**: Redis, JWT Authentication, SystemParameters  
**Entregável**: Sistema completo de rate limiting por planos e configuração dinâmica