# Fase 4 - Parte 4: Cache Strategy Implementation

## Contexto da Implementação

Esta é a **quarta parte da Fase 4** focada na **implementação completa da estratégia de cache** com Redis VM para otimizar performance de workspaces, sessões e dados de tempo real.

### Objetivos da Parte 4
✅ **Redis VM** configuration otimizada  
✅ **Cache service** avançado com múltiplas operações  
✅ **Cache patterns** para workspaces e sessions  
✅ **Performance monitoring** do cache  
✅ **Cache invalidation** strategy inteligente  
✅ **Memory management** otimizado  

### Pré-requisitos
- Partes 1-3 implementadas e funcionais
- Redis VM/Container configurado
- ApplicationDbContext funcionando

---

## 3.1 Redis VM Configuration

Configuração otimizada do Redis para caching de workspaces, sessões e dados de tempo real.

### IDE.Infrastructure/Caching/RedisCacheService.cs
```csharp
// IRedisCacheService já definida no arquivo 04-01-frontend-service-integration.md

public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
        _server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            
            if (!value.HasValue)
                return default(T);

            return JsonSerializer.Deserialize<T>(value, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            
            await _database.StringSetAsync(key, serializedValue, expiry);
            _logger.LogDebug("Cache key {Key} set with expiry {Expiry}", key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}", key);
        }
    }

    public async Task<List<string>> GetKeysAsync(string pattern)
    {
        try
        {
            return _server.Keys(pattern: pattern).Select(k => k.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting keys with pattern {Pattern}", pattern);
            return new List<string>();
        }
    }

    public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiry = null)
    {
        try
        {
            var result = await _database.StringIncrementAsync(key, value);
            
            if (expiry.HasValue)
            {
                await _database.KeyExpireAsync(key, expiry.Value);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing cache key {Key}", key);
            return 0;
        }
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var result = await _database.StringSetAsync(key, serializedValue, expiry, When.NotExists);
            
            if (result)
            {
                _logger.LogDebug("Cache key {Key} set (if not exists) with expiry {Expiry}", key, expiry);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key} if not exists", key);
            return false;
        }
    }

    public async Task<T> GetAndDeleteAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetDeleteAsync(key);
            
            if (!value.HasValue)
                return default(T);

            return JsonSerializer.Deserialize<T>(value, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting and deleting cache key {Key}", key);
            return default(T);
        }
    }

    public async Task<List<T>> GetMultipleAsync<T>(IEnumerable<string> keys)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var values = await _database.StringGetAsync(redisKeys);
            
            var results = new List<T>();
            foreach (var value in values)
            {
                if (value.HasValue)
                {
                    var item = JsonSerializer.Deserialize<T>(value);
                    results.Add(item);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple cache keys");
            return new List<T>();
        }
    }

    public async Task SetMultipleAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiry = null)
    {
        try
        {
            var pipeline = _database.CreateBatch();
            var tasks = new List<Task>();

            foreach (var kvp in keyValuePairs)
            {
                var serializedValue = JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                
                tasks.Add(pipeline.StringSetAsync(kvp.Key, serializedValue, expiry));
            }

            pipeline.Execute();
            await Task.WhenAll(tasks);
            
            _logger.LogDebug("Set {Count} cache keys with expiry {Expiry}", keyValuePairs.Count, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting multiple cache keys");
        }
    }

    public async Task<long> GetTtlAsync(string key)
    {
        try
        {
            var ttl = await _database.KeyTimeToLiveAsync(key);
            return ttl?.Ticks ?? -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TTL for cache key {Key}", key);
            return -1;
        }
    }

    public async Task<bool> RefreshTtlAsync(string key, TimeSpan expiry)
    {
        try
        {
            return await _database.KeyExpireAsync(key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing TTL for cache key {Key}", key);
            return false;
        }
    }

    // Implementation of other ICacheService methods...
    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Cache key {Key} removed", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if cache key {Key} exists", key);
            return false;
        }
    }
}
```

### Cache Configuration e Dependency Injection

#### IDE.Infrastructure/Configuration/CacheConfiguration.cs
```csharp
public static class CacheConfiguration
{
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Redis connection string is required");
        }

        // Configure Redis connection
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.ReconnectRetryPolicy = new ExponentialRetry(1000, 30000);
            options.ConnectRetry = 5;
            options.ConnectTimeout = 10000;
            options.SyncTimeout = 5000;
            options.AbortOnConnectFail = false;
            
            return ConnectionMultiplexer.Connect(options);
        });

        // Register cache services
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();
        services.AddSingleton<ICachePerformanceMonitor, CachePerformanceMonitor>();

        // Configure cache policies
        services.Configure<CacheOptions>(options =>
        {
            options.DefaultExpiry = TimeSpan.FromMinutes(30);
            options.WorkspaceExpiry = TimeSpan.FromMinutes(15);
            options.SessionExpiry = TimeSpan.FromHours(8);
            options.UserPresenceExpiry = TimeSpan.FromMinutes(5);
            options.ChatExpiry = TimeSpan.FromHours(1);
        });

        return services;
    }
}

public class CacheOptions
{
    public TimeSpan DefaultExpiry { get; set; }
    public TimeSpan WorkspaceExpiry { get; set; }
    public TimeSpan SessionExpiry { get; set; }
    public TimeSpan UserPresenceExpiry { get; set; }
    public TimeSpan ChatExpiry { get; set; }
}
```

### Cache Invalidation Service

#### IDE.Infrastructure/Caching/CacheInvalidationService.cs
```csharp
// ICacheInvalidationService já definida no arquivo 04-01-frontend-service-integration.md

public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IRedisCacheService _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(IRedisCacheService cache, ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvalidateWorkspaceAsync(Guid workspaceId)
    {
        var patterns = new[]
        {
            $"workspace:{workspaceId}*",
            $"items:{workspaceId}*",
            $"permissions:{workspaceId}*",
            $"chat:{workspaceId}*",
            $"presence:user:{workspaceId}*"
        };

        var tasks = patterns.Select(InvalidatePatternAsync);
        await Task.WhenAll(tasks);

        _logger.LogInformation("Invalidated cache for workspace {WorkspaceId}", workspaceId);
    }

    public async Task InvalidateUserAsync(Guid userId)
    {
        var patterns = new[]
        {
            $"user:{userId}*",
            $"permissions:user:{userId}*",
            $"presence:*:{userId}*",
            $"session:*:{userId}*"
        };

        var tasks = patterns.Select(InvalidatePatternAsync);
        await Task.WhenAll(tasks);

        _logger.LogInformation("Invalidated cache for user {UserId}", userId);
    }

    public async Task InvalidatePatternAsync(string pattern)
    {
        try
        {
            var keys = await _cache.GetKeysAsync(pattern);
            
            if (keys.Any())
            {
                await InvalidateMultipleAsync(keys);
                _logger.LogDebug("Invalidated {Count} keys matching pattern {Pattern}", keys.Count, pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating pattern {Pattern}", pattern);
        }
    }

    public async Task InvalidateMultipleAsync(IEnumerable<string> keys)
    {
        try
        {
            var tasks = keys.Select(key => _cache.RemoveAsync(key));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating multiple cache keys");
        }
    }

    public async Task InvalidateWorkspaceItemsAsync(Guid workspaceId)
    {
        await InvalidatePatternAsync($"items:{workspaceId}*");
        await InvalidatePatternAsync($"navigation:{workspaceId}*");
        
        _logger.LogDebug("Invalidated workspace items cache for {WorkspaceId}", workspaceId);
    }

    public async Task InvalidateChatAsync(Guid workspaceId)
    {
        await InvalidatePatternAsync($"chat:{workspaceId}*");
        
        _logger.LogDebug("Invalidated chat cache for {WorkspaceId}", workspaceId);
    }
}
```

### Cache Performance Monitor

#### IDE.Infrastructure/Caching/ICachePerformanceMonitor.cs
```csharp
public interface ICachePerformanceMonitor
{
    void RecordHit(string key);
    void RecordMiss(string key);
    void RecordOperation(string operation, TimeSpan duration);
    Task<CacheMetrics> GetMetricsAsync();
    Task<Dictionary<string, CacheKeyMetrics>> GetKeyMetricsAsync();
}

public class CachePerformanceMonitor : ICachePerformanceMonitor
{
    private readonly IRedisCacheService _cache;
    private readonly ILogger<CachePerformanceMonitor> _logger;
    private readonly ConcurrentDictionary<string, CacheKeyMetrics> _keyMetrics;
    private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics;

    public CachePerformanceMonitor(IRedisCacheService cache, ILogger<CachePerformanceMonitor> logger)
    {
        _cache = cache;
        _logger = logger;
        _keyMetrics = new ConcurrentDictionary<string, CacheKeyMetrics>();
        _operationMetrics = new ConcurrentDictionary<string, OperationMetrics>();
    }

    public void RecordHit(string key)
    {
        var pattern = ExtractPattern(key);
        _keyMetrics.AddOrUpdate(pattern, 
            new CacheKeyMetrics { Pattern = pattern, Hits = 1 },
            (k, existing) => { existing.Hits++; return existing; });
    }

    public void RecordMiss(string key)
    {
        var pattern = ExtractPattern(key);
        _keyMetrics.AddOrUpdate(pattern,
            new CacheKeyMetrics { Pattern = pattern, Misses = 1 },
            (k, existing) => { existing.Misses++; return existing; });
    }

    public void RecordOperation(string operation, TimeSpan duration)
    {
        _operationMetrics.AddOrUpdate(operation,
            new OperationMetrics 
            { 
                Operation = operation, 
                TotalTime = duration, 
                Count = 1,
                MinTime = duration,
                MaxTime = duration
            },
            (k, existing) => 
            {
                existing.TotalTime = existing.TotalTime.Add(duration);
                existing.Count++;
                existing.MinTime = duration < existing.MinTime ? duration : existing.MinTime;
                existing.MaxTime = duration > existing.MaxTime ? duration : existing.MaxTime;
                return existing;
            });
    }

    public async Task<CacheMetrics> GetMetricsAsync()
    {
        var totalHits = _keyMetrics.Values.Sum(m => m.Hits);
        var totalMisses = _keyMetrics.Values.Sum(m => m.Misses);
        var hitRate = totalHits + totalMisses > 0 ? (double)totalHits / (totalHits + totalMisses) : 0;

        // Get Redis info
        var info = await GetRedisInfoAsync();

        return new CacheMetrics
        {
            TotalHits = totalHits,
            TotalMisses = totalMisses,
            HitRate = hitRate,
            TotalKeys = _keyMetrics.Count,
            UsedMemory = info.UsedMemory,
            MaxMemory = info.MaxMemory,
            ConnectedClients = info.ConnectedClients,
            OperationsPerSecond = CalculateOpsPerSecond(),
            AverageResponseTime = CalculateAverageResponseTime(),
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<Dictionary<string, CacheKeyMetrics>> GetKeyMetricsAsync()
    {
        return _keyMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private string ExtractPattern(string key)
    {
        // Extract pattern from key (e.g., "workspace:123:items" -> "workspace:*:items")
        var segments = key.Split(':');
        for (int i = 1; i < segments.Length - 1; i++)
        {
            if (Guid.TryParse(segments[i], out _) || int.TryParse(segments[i], out _))
            {
                segments[i] = "*";
            }
        }
        return string.Join(":", segments);
    }

    private async Task<RedisInfo> GetRedisInfoAsync()
    {
        try
        {
            // Get Redis server info
            var server = _cache._connectionMultiplexer.GetServer(_cache._connectionMultiplexer.GetEndPoints().First());
            var info = await server.InfoAsync();
            
            return new RedisInfo
            {
                UsedMemory = ExtractInfoValue(info, "used_memory"),
                MaxMemory = ExtractInfoValue(info, "maxmemory"),
                ConnectedClients = (int)ExtractInfoValue(info, "connected_clients")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Redis info");
            return new RedisInfo();
        }
    }

    private long ExtractInfoValue(IGrouping<string, KeyValuePair<string, string>>[] info, string key)
    {
        var memory = info.FirstOrDefault(g => g.Key == "Memory");
        if (memory != null)
        {
            var pair = memory.FirstOrDefault(kvp => kvp.Key == key);
            if (long.TryParse(pair.Value, out var value))
                return value;
        }
        return 0;
    }

    private double CalculateOpsPerSecond()
    {
        var totalOps = _operationMetrics.Values.Sum(m => m.Count);
        // Simplified calculation - in reality, you'd track time windows
        return totalOps / Math.Max(1, DateTime.UtcNow.Subtract(DateTime.Today).TotalSeconds);
    }

    private TimeSpan CalculateAverageResponseTime()
    {
        var operations = _operationMetrics.Values.Where(m => m.Count > 0);
        if (!operations.Any()) return TimeSpan.Zero;

        var avgTicks = operations.Average(m => m.TotalTime.Ticks / m.Count);
        return new TimeSpan((long)avgTicks);
    }
}

public class CacheMetrics
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double HitRate { get; set; }
    public int TotalKeys { get; set; }
    public long UsedMemory { get; set; }
    public long MaxMemory { get; set; }
    public int ConnectedClients { get; set; }
    public double OperationsPerSecond { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CacheKeyMetrics
{
    public string Pattern { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
}

public class OperationMetrics
{
    public string Operation { get; set; }
    public TimeSpan TotalTime { get; set; }
    public long Count { get; set; }
    public TimeSpan MinTime { get; set; }
    public TimeSpan MaxTime { get; set; }
    public TimeSpan AverageTime => Count > 0 ? new TimeSpan(TotalTime.Ticks / Count) : TimeSpan.Zero;
}

public class RedisInfo
{
    public long UsedMemory { get; set; }
    public long MaxMemory { get; set; }
    public int ConnectedClients { get; set; }
}
```

### Cache Warmup Service

#### IDE.Infrastructure/Caching/ICacheWarmupService.cs
```csharp
public interface ICacheWarmupService
{
    Task WarmupWorkspaceAsync(Guid workspaceId);
    Task WarmupUserDataAsync(Guid userId);
    Task WarmupSystemDataAsync();
    Task ScheduledWarmupAsync();
}

public class CacheWarmupService : ICacheWarmupService
{
    private readonly ApplicationDbContext _context;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<CacheWarmupService> _logger;
    private readonly CacheOptions _cacheOptions;

    public CacheWarmupService(
        ApplicationDbContext context,
        IRedisCacheService cache,
        ILogger<CacheWarmupService> logger,
        IOptions<CacheOptions> cacheOptions)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task WarmupWorkspaceAsync(Guid workspaceId)
    {
        try
        {
            // Preload workspace data
            var workspace = await _context.Workspaces
                .Include(w => w.ModuleItems)
                .Include(w => w.Permissions)
                .FirstOrDefaultAsync(w => w.Id == workspaceId);

            if (workspace != null)
            {
                await _cache.SetAsync($"workspace:{workspaceId}", workspace, _cacheOptions.WorkspaceExpiry);

                // Preload module items
                var itemsGrouped = workspace.ModuleItems.GroupBy(i => i.Module);
                foreach (var group in itemsGrouped)
                {
                    await _cache.SetAsync(
                        $"items:{workspaceId}:{group.Key}", 
                        group.ToList(), 
                        _cacheOptions.WorkspaceExpiry);
                }

                // Preload permissions
                await _cache.SetAsync(
                    $"permissions:{workspaceId}", 
                    workspace.Permissions.ToList(), 
                    _cacheOptions.WorkspaceExpiry);

                _logger.LogInformation("Warmed up cache for workspace {WorkspaceId}", workspaceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error warming up workspace {WorkspaceId}", workspaceId);
        }
    }

    public async Task WarmupUserDataAsync(Guid userId)
    {
        try
        {
            // Preload user data
            var user = await _context.Users
                .Include(u => u.WorkspacePermissions)
                .ThenInclude(wp => wp.Workspace)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null)
            {
                await _cache.SetAsync($"user:{userId}", user, _cacheOptions.DefaultExpiry);

                // Preload user workspaces
                var workspaces = user.WorkspacePermissions.Select(wp => wp.Workspace).ToList();
                await _cache.SetAsync($"user:{userId}:workspaces", workspaces, _cacheOptions.WorkspaceExpiry);

                _logger.LogInformation("Warmed up cache for user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error warming up user {UserId}", userId);
        }
    }

    public async Task WarmupSystemDataAsync()
    {
        try
        {
            // Preload frequently accessed system data
            var systemParams = await _context.SystemParameters.ToListAsync();
            await _cache.SetMultipleAsync(
                systemParams.ToDictionary(
                    p => $"system:param:{p.Key}", 
                    p => p.Value
                ), 
                TimeSpan.FromHours(1)
            );

            _logger.LogInformation("Warmed up system cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error warming up system cache");
        }
    }

    public async Task ScheduledWarmupAsync()
    {
        // Warmup most active workspaces
        var activeWorkspaces = await _context.CollaborationSessions
            .Where(s => s.LastActivity > DateTime.UtcNow.AddHours(-1))
            .Select(s => s.WorkspaceId)
            .Distinct()
            .Take(20)
            .ToListAsync();

        var warmupTasks = activeWorkspaces.Select(WarmupWorkspaceAsync);
        await Task.WhenAll(warmupTasks);

        _logger.LogInformation("Scheduled warmup completed for {Count} workspaces", activeWorkspaces.Count);
    }
}
```

---

## Entregáveis da Parte 4

### ✅ Implementações Completas
- **IRedisCacheService** com operações avançadas
- **CacheInvalidationService** para invalidação inteligente
- **CachePerformanceMonitor** com métricas detalhadas  
- **CacheWarmupService** para preload estratégico
- **CacheConfiguration** com políticas otimizadas

### ✅ Funcionalidades de Cache
- **Multi-operations** (get/set multiple keys)
- **Pattern-based** invalidation
- **TTL management** dinâmico
- **Performance monitoring** em tempo real
- **Cache warmup** automático
- **Memory management** otimizado

### ✅ Estratégias Implementadas
- **Write-through** para dados críticos
- **Write-behind** para dados de sessão
- **Cache-aside** para dados consultados
- **Time-based** invalidation
- **Pattern-based** invalidation
- **Batch operations** para performance

---

## Validação da Parte 4

### Critérios de Sucesso
- [ ] Redis conecta sem erros
- [ ] Cache operations respondem < 50ms
- [ ] Hit rate > 80% para dados frequentes
- [ ] Invalidation patterns funcionam corretamente
- [ ] Performance metrics são coletadas
- [ ] Memory usage está controlado
- [ ] Warmup strategies são efetivas

### Testes de Cache
```bash
# 1. Testar conexão Redis
redis-cli ping

# 2. Testar cache via API
curl -X GET http://localhost:8503/api/cache/metrics \
  -H "Authorization: Bearer <token>"

# 3. Testar invalidação
curl -X POST http://localhost:8503/api/cache/invalidate/workspace/<workspace-id> \
  -H "Authorization: Bearer <token>"
```

### Performance Targets
- **Get operations**: < 10ms (p95)
- **Set operations**: < 20ms (p95)
- **Hit rate**: > 80% overall
- **Memory usage**: < 1GB for 1000 users
- **Connection count**: < 100 concurrent

---

## Próximos Passos

Após validação da Parte 4, prosseguir para:
- **Parte 5**: Database & API Optimization

---

**Tempo Estimado**: 2-3 horas  
**Complexidade**: Média  
**Dependências**: Redis VM, ApplicationDbContext  
**Entregável**: Sistema de cache Redis otimizado e monitorado