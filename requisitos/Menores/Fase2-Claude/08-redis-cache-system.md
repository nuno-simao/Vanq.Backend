# Parte 8: Redis Cache System - Workspace Core

## Contexto
Esta é a **Parte 8 de 12** da Fase 2 (Workspace Core). Aqui implementaremos o sistema completo de cache Redis para otimizar a performance das operações de workspace.

**Pré-requisitos**: Parte 7 (Requests e Validações) deve estar concluída

**Dependências**: Redis server, StackExchange.Redis

**Próxima parte**: Parte 9 - SignalR Hub Básico

## Objetivos desta Parte
✅ Implementar RedisCacheService completo  
✅ Definir estratégias de cache por entidade  
✅ Configurar TTL apropriados  
✅ Implementar cache invalidation  
✅ Otimizar performance de queries  

## 1. RedisCacheService Implementation

### 1.1 IRedisCacheService.cs

#### IDE.Application/Common/Interfaces/IRedisCacheService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDE.Application.Common.Interfaces
{
    public interface IRedisCacheService
    {
        // Basic operations
        Task<T> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemovePatternAsync(string pattern);
        Task<bool> ExistsAsync(string key);
        
        // Batch operations
        Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys) where T : class;
        Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiration = null) where T : class;
        
        // List operations
        Task<List<T>> GetListAsync<T>(string key) where T : class;
        Task SetListAsync<T>(string key, List<T> items, TimeSpan? expiration = null) where T : class;
        Task AddToListAsync<T>(string key, T item) where T : class;
        
        // Hash operations
        Task<T> GetHashFieldAsync<T>(string key, string field) where T : class;
        Task SetHashFieldAsync<T>(string key, string field, T value) where T : class;
        Task<Dictionary<string, T>> GetHashAsync<T>(string key) where T : class;
        
        // Cache statistics
        Task<long> GetKeysCountAsync(string pattern = "*");
        Task<List<string>> GetKeysAsync(string pattern = "*", int limit = 100);
        
        // Utilities
        string GenerateKey(params string[] parts);
        Task FlushDatabaseAsync(); // Only for development
    }
}
```

### 1.2 RedisCacheService.cs

#### IDE.Infrastructure/Cache/RedisCacheService.cs
```csharp
using IDE.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace IDE.Infrastructure.Cache
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDatabase _database;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _keyPrefix = "ide_workspace";

        public RedisCacheService(
            IConnectionMultiplexer redis, 
            ILogger<RedisCacheService> logger)
        {
            _database = redis.GetDatabase();
            _redis = redis;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            try
            {
                var value = await _database.StringGetAsync(GenerateKey(key));
                if (!value.HasValue)
                    return null;

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar chave {Key} no cache", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await _database.StringSetAsync(GenerateKey(key), json, expiration);
                
                _logger.LogDebug("Cache definido para chave {Key}, TTL: {TTL}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir chave {Key} no cache", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _database.KeyDeleteAsync(GenerateKey(key));
                _logger.LogDebug("Chave {Key} removida do cache", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover chave {Key} do cache", key);
            }
        }

        public async Task RemovePatternAsync(string pattern)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: GenerateKey(pattern)).ToArray();
                
                if (keys.Length > 0)
                {
                    await _database.KeyDeleteAsync(keys);
                    _logger.LogDebug("Removidas {Count} chaves com padrão {Pattern}", keys.Length, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover padrão {Pattern} do cache", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(GenerateKey(key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar existência da chave {Key}", key);
                return false;
            }
        }

        public async Task<Dictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys) where T : class
        {
            try
            {
                var redisKeys = keys.Select(k => (RedisKey)GenerateKey(k)).ToArray();
                var values = await _database.StringGetAsync(redisKeys);
                
                var result = new Dictionary<string, T>();
                var keyArray = keys.ToArray();
                
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i].HasValue)
                    {
                        var item = JsonSerializer.Deserialize<T>(values[i], _jsonOptions);
                        result[keyArray[i]] = item;
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar múltiplas chaves no cache");
                return new Dictionary<string, T>();
            }
        }

        public async Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var batch = _database.CreateBatch();
                var tasks = new List<Task>();

                foreach (var item in items)
                {
                    var json = JsonSerializer.Serialize(item.Value, _jsonOptions);
                    tasks.Add(batch.StringSetAsync(GenerateKey(item.Key), json, expiration));
                }

                batch.Execute();
                await Task.WhenAll(tasks);
                
                _logger.LogDebug("Cache definido para {Count} itens em lote", items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir múltiplas chaves no cache");
            }
        }

        public async Task<List<T>> GetListAsync<T>(string key) where T : class
        {
            try
            {
                var values = await _database.ListRangeAsync(GenerateKey(key));
                return values.Select(v => JsonSerializer.Deserialize<T>(v, _jsonOptions)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar lista {Key} no cache", key);
                return new List<T>();
            }
        }

        public async Task SetListAsync<T>(string key, List<T> items, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var redisKey = GenerateKey(key);
                await _database.KeyDeleteAsync(redisKey);
                
                var values = items.Select(item => (RedisValue)JsonSerializer.Serialize(item, _jsonOptions)).ToArray();
                await _database.ListRightPushAsync(redisKey, values);
                
                if (expiration.HasValue)
                    await _database.KeyExpireAsync(redisKey, expiration);
                    
                _logger.LogDebug("Lista {Key} definida no cache com {Count} itens", key, items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir lista {Key} no cache", key);
            }
        }

        public async Task AddToListAsync<T>(string key, T item) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(item, _jsonOptions);
                await _database.ListRightPushAsync(GenerateKey(key), json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar item à lista {Key}", key);
            }
        }

        public async Task<T> GetHashFieldAsync<T>(string key, string field) where T : class
        {
            try
            {
                var value = await _database.HashGetAsync(GenerateKey(key), field);
                if (!value.HasValue)
                    return null;

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar campo {Field} do hash {Key}", field, key);
                return null;
            }
        }

        public async Task SetHashFieldAsync<T>(string key, string field, T value) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await _database.HashSetAsync(GenerateKey(key), field, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir campo {Field} do hash {Key}", field, key);
            }
        }

        public async Task<Dictionary<string, T>> GetHashAsync<T>(string key) where T : class
        {
            try
            {
                var hash = await _database.HashGetAllAsync(GenerateKey(key));
                var result = new Dictionary<string, T>();
                
                foreach (var item in hash)
                {
                    var value = JsonSerializer.Deserialize<T>(item.Value, _jsonOptions);
                    result[item.Name] = value;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar hash {Key}", key);
                return new Dictionary<string, T>();
            }
        }

        public async Task<long> GetKeysCountAsync(string pattern = "*")
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                return server.Keys(pattern: GenerateKey(pattern)).LongCount();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao contar chaves com padrão {Pattern}", pattern);
                return 0;
            }
        }

        public async Task<List<string>> GetKeysAsync(string pattern = "*", int limit = 100)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                return server.Keys(pattern: GenerateKey(pattern))
                    .Take(limit)
                    .Select(k => k.ToString().Replace($"{_keyPrefix}:", ""))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar chaves com padrão {Pattern}", pattern);
                return new List<string>();
            }
        }

        public string GenerateKey(params string[] parts)
        {
            return $"{_keyPrefix}:{string.Join(":", parts)}";
        }

        public async Task FlushDatabaseAsync()
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                await server.FlushDatabaseAsync();
                _logger.LogWarning("Cache Redis foi completamente limpo (apenas desenvolvimento)");
            }
        }
    }
}
```

## 2. Cache Strategies

### 2.1 CacheKeyBuilder.cs

#### IDE.Infrastructure/Cache/CacheKeyBuilder.cs
```csharp
using System;

namespace IDE.Infrastructure.Cache
{
    public static class CacheKeyBuilder
    {
        // Workspace keys
        public static string WorkspaceKey(Guid workspaceId) => $"workspace:{workspaceId}";
        public static string WorkspaceListKey(Guid userId, int page, int pageSize) => $"workspace:list:user:{userId}:page:{page}:size:{pageSize}";
        public static string WorkspaceSummaryKey(Guid workspaceId) => $"workspace:summary:{workspaceId}";
        
        // Item keys
        public static string ItemKey(Guid itemId) => $"item:{itemId}";
        public static string ItemListKey(Guid workspaceId, string module = null, string type = null, int page = 1, int pageSize = 50)
        {
            var key = $"items:workspace:{workspaceId}";
            if (!string.IsNullOrEmpty(module)) key += $":module:{module}";
            if (!string.IsNullOrEmpty(type)) key += $":type:{type}";
            key += $":page:{page}:size:{pageSize}";
            return key;
        }
        public static string ItemTreeKey(Guid workspaceId, string module) => $"items:tree:workspace:{workspaceId}:module:{module}";
        
        // Permission keys
        public static string UserPermissionKey(Guid userId, Guid workspaceId) => $"permission:user:{userId}:workspace:{workspaceId}";
        public static string WorkspacePermissionsKey(Guid workspaceId) => $"permissions:workspace:{workspaceId}";
        
        // Navigation state keys
        public static string NavigationStateKey(Guid userId, Guid workspaceId, string moduleId) => 
            $"navigation:user:{userId}:workspace:{workspaceId}:module:{moduleId}";
        
        // Activity log keys
        public static string ActivityLogKey(Guid workspaceId, int page, int pageSize) => 
            $"activity:workspace:{workspaceId}:page:{page}:size:{pageSize}";
        
        // System parameter keys
        public static string SystemParameterKey(string key) => $"param:{key}";
        public static string SystemParametersCategoryKey(string category) => $"params:category:{category}";
        
        // Tag keys
        public static string WorkspaceTagsKey(Guid workspaceId) => $"tags:workspace:{workspaceId}";
        public static string ItemTagsKey(Guid itemId) => $"tags:item:{itemId}";
        
        // Version keys
        public static string WorkspaceVersionsKey(Guid workspaceId) => $"versions:workspace:{workspaceId}";
        public static string ItemVersionsKey(Guid itemId) => $"versions:item:{itemId}";
        
        // Utility methods
        public static string UserWorkspacesPattern(Guid userId) => $"workspace:*user:{userId}*";
        public static string WorkspacePattern(Guid workspaceId) => $"*workspace:{workspaceId}*";
        public static string ItemPattern(Guid itemId) => $"*item:{itemId}*";
    }
}
```

### 2.2 CacheConfiguration.cs

#### IDE.Infrastructure/Cache/CacheConfiguration.cs
```csharp
using System;

namespace IDE.Infrastructure.Cache
{
    public static class CacheConfiguration
    {
        // TTL configurations
        public static readonly TimeSpan WorkspaceCacheTTL = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan WorkspaceListCacheTTL = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan ItemCacheTTL = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan ItemListCacheTTL = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan PermissionCacheTTL = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan NavigationStateCacheTTL = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan ActivityLogCacheTTL = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan SystemParameterCacheTTL = TimeSpan.FromHours(24);
        public static readonly TimeSpan TagsCacheTTL = TimeSpan.FromMinutes(60);
        public static readonly TimeSpan VersionsCacheTTL = TimeSpan.FromMinutes(30);
        
        // Size limits
        public const int MaxItemsPerList = 1000;
        public const int MaxNavigationStateSize = 100_000; // 100KB
        public const int MaxCacheKeyLength = 250;
        
        // Prefixes
        public const string KeyPrefix = "ide_workspace";
        public const string LockPrefix = "lock";
        
        // Cache warming settings
        public static readonly TimeSpan CacheWarmupInterval = TimeSpan.FromHours(6);
        public static readonly int CacheWarmupBatchSize = 50;
    }
}
```

## 3. Cache Invalidation Service

### 3.1 ICacheInvalidationService.cs

#### IDE.Application/Common/Interfaces/ICacheInvalidationService.cs
```csharp
using System;
using System.Threading.Tasks;

namespace IDE.Application.Common.Interfaces
{
    public interface ICacheInvalidationService
    {
        // Workspace invalidation
        Task InvalidateWorkspaceAsync(Guid workspaceId);
        Task InvalidateUserWorkspacesAsync(Guid userId);
        
        // Item invalidation
        Task InvalidateItemAsync(Guid workspaceId, Guid itemId);
        Task InvalidateWorkspaceItemsAsync(Guid workspaceId);
        
        // Permission invalidation
        Task InvalidatePermissionsAsync(Guid workspaceId);
        Task InvalidateUserPermissionAsync(Guid userId, Guid workspaceId);
        
        // Navigation state invalidation
        Task InvalidateNavigationStateAsync(Guid userId, Guid workspaceId, string moduleId = null);
        
        // Activity log invalidation
        Task InvalidateActivityLogsAsync(Guid workspaceId);
        
        // System parameter invalidation
        Task InvalidateSystemParametersAsync(string category = null);
        
        // Tag invalidation
        Task InvalidateTagsAsync(Guid workspaceId);
        Task InvalidateItemTagsAsync(Guid itemId);
        
        // Bulk invalidation
        Task InvalidateAllWorkspaceDataAsync(Guid workspaceId);
        Task InvalidateAllUserDataAsync(Guid userId);
    }
}
```

### 3.2 CacheInvalidationService.cs

#### IDE.Infrastructure/Cache/CacheInvalidationService.cs
```csharp
using IDE.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IDE.Infrastructure.Cache
{
    public class CacheInvalidationService : ICacheInvalidationService
    {
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<CacheInvalidationService> _logger;

        public CacheInvalidationService(
            IRedisCacheService cacheService,
            ILogger<CacheInvalidationService> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task InvalidateWorkspaceAsync(Guid workspaceId)
        {
            await _cacheService.RemoveAsync(CacheKeyBuilder.WorkspaceKey(workspaceId));
            await _cacheService.RemoveAsync(CacheKeyBuilder.WorkspaceSummaryKey(workspaceId));
            await _cacheService.RemovePatternAsync($"workspace:list:*");
            
            _logger.LogDebug("Cache do workspace {WorkspaceId} invalidado", workspaceId);
        }

        public async Task InvalidateUserWorkspacesAsync(Guid userId)
        {
            await _cacheService.RemovePatternAsync(CacheKeyBuilder.UserWorkspacesPattern(userId));
            _logger.LogDebug("Cache de workspaces do usuário {UserId} invalidado", userId);
        }

        public async Task InvalidateItemAsync(Guid workspaceId, Guid itemId)
        {
            await _cacheService.RemoveAsync(CacheKeyBuilder.ItemKey(itemId));
            await _cacheService.RemovePatternAsync(CacheKeyBuilder.ItemPattern(itemId));
            await InvalidateWorkspaceItemsAsync(workspaceId);
            
            _logger.LogDebug("Cache do item {ItemId} invalidado", itemId);
        }

        public async Task InvalidateWorkspaceItemsAsync(Guid workspaceId)
        {
            await _cacheService.RemovePatternAsync($"items:workspace:{workspaceId}:*");
            await _cacheService.RemovePatternAsync($"items:tree:workspace:{workspaceId}:*");
            
            _logger.LogDebug("Cache de items do workspace {WorkspaceId} invalidado", workspaceId);
        }

        public async Task InvalidatePermissionsAsync(Guid workspaceId)
        {
            await _cacheService.RemoveAsync(CacheKeyBuilder.WorkspacePermissionsKey(workspaceId));
            await _cacheService.RemovePatternAsync($"permission:*workspace:{workspaceId}");
            
            _logger.LogDebug("Cache de permissões do workspace {WorkspaceId} invalidado", workspaceId);
        }

        public async Task InvalidateUserPermissionAsync(Guid userId, Guid workspaceId)
        {
            await _cacheService.RemoveAsync(CacheKeyBuilder.UserPermissionKey(userId, workspaceId));
            _logger.LogDebug("Cache de permissão do usuário {UserId} no workspace {WorkspaceId} invalidado", userId, workspaceId);
        }

        public async Task InvalidateNavigationStateAsync(Guid userId, Guid workspaceId, string moduleId = null)
        {
            if (string.IsNullOrEmpty(moduleId))
            {
                await _cacheService.RemovePatternAsync($"navigation:user:{userId}:workspace:{workspaceId}:*");
            }
            else
            {
                await _cacheService.RemoveAsync(CacheKeyBuilder.NavigationStateKey(userId, workspaceId, moduleId));
            }
            
            _logger.LogDebug("Cache de navegação invalidado para usuário {UserId}, workspace {WorkspaceId}, módulo {ModuleId}", 
                userId, workspaceId, moduleId ?? "todos");
        }

        public async Task InvalidateActivityLogsAsync(Guid workspaceId)
        {
            await _cacheService.RemovePatternAsync($"activity:workspace:{workspaceId}:*");
            _logger.LogDebug("Cache de activity logs do workspace {WorkspaceId} invalidado", workspaceId);
        }

        public async Task InvalidateSystemParametersAsync(string category = null)
        {
            if (string.IsNullOrEmpty(category))
            {
                await _cacheService.RemovePatternAsync("param:*");
                await _cacheService.RemovePatternAsync("params:*");
            }
            else
            {
                await _cacheService.RemoveAsync(CacheKeyBuilder.SystemParametersCategoryKey(category));
            }
            
            _logger.LogDebug("Cache de parâmetros do sistema invalidado, categoria: {Category}", category ?? "todas");
        }

        public async Task InvalidateTagsAsync(Guid workspaceId)
        {
            await _cacheService.RemoveAsync(CacheKeyBuilder.WorkspaceTagsKey(workspaceId));
            _logger.LogDebug("Cache de tags do workspace {WorkspaceId} invalidado", workspaceId);
        }

        public async Task InvalidateItemTagsAsync(Guid itemId)
        {
            await _cacheService.RemoveAsync(CacheKeyBuilder.ItemTagsKey(itemId));
            _logger.LogDebug("Cache de tags do item {ItemId} invalidado", itemId);
        }

        public async Task InvalidateAllWorkspaceDataAsync(Guid workspaceId)
        {
            await _cacheService.RemovePatternAsync(CacheKeyBuilder.WorkspacePattern(workspaceId));
            _logger.LogDebug("Todos os caches do workspace {WorkspaceId} invalidados", workspaceId);
        }

        public async Task InvalidateAllUserDataAsync(Guid userId)
        {
            await _cacheService.RemovePatternAsync($"*user:{userId}*");
            _logger.LogDebug("Todos os caches do usuário {UserId} invalidados", userId);
        }
    }
}
```

## 4. Cache Warming Service

### 4.1 CacheWarmupService.cs

#### IDE.Infrastructure/Cache/CacheWarmupService.cs
```csharp
using IDE.Application.Common.Interfaces;
using IDE.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IDE.Infrastructure.Cache
{
    public class CacheWarmupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheWarmupService> _logger;

        public CacheWarmupService(
            IServiceProvider serviceProvider,
            ILogger<CacheWarmupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Aguardar inicialização completa
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await WarmupCache();
                    await Task.Delay(CacheConfiguration.CacheWarmupInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro durante warmup do cache");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
        }

        private async Task WarmupCache()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cacheService = scope.ServiceProvider.GetRequiredService<IRedisCacheService>();

            _logger.LogInformation("Iniciando warmup do cache");

            // Warmup system parameters
            await WarmupSystemParameters(context, cacheService);

            // Warmup active workspaces
            await WarmupActiveWorkspaces(context, cacheService);

            _logger.LogInformation("Warmup do cache concluído");
        }

        private async Task WarmupSystemParameters(ApplicationDbContext context, IRedisCacheService cacheService)
        {
            try
            {
                var parameters = await context.SystemParameters
                    .Where(p => p.IsActive)
                    .ToListAsync();

                var tasks = parameters.Select(async param =>
                {
                    var key = CacheKeyBuilder.SystemParameterKey(param.Key);
                    if (!await cacheService.ExistsAsync(key))
                    {
                        await cacheService.SetAsync(key, param.Value, CacheConfiguration.SystemParameterCacheTTL);
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogDebug("System parameters warming concluído: {Count} parâmetros", parameters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no warmup de system parameters");
            }
        }

        private async Task WarmupActiveWorkspaces(ApplicationDbContext context, IRedisCacheService cacheService)
        {
            try
            {
                var recentWorkspaces = await context.Workspaces
                    .Where(w => !w.IsArchived && w.UpdatedAt > DateTime.UtcNow.AddDays(-7))
                    .Include(w => w.Owner)
                    .Include(w => w.CurrentPhase)
                    .Take(CacheConfiguration.CacheWarmupBatchSize)
                    .ToListAsync();

                var tasks = recentWorkspaces.Select(async workspace =>
                {
                    var summaryKey = CacheKeyBuilder.WorkspaceSummaryKey(workspace.Id);
                    if (!await cacheService.ExistsAsync(summaryKey))
                    {
                        var summary = new
                        {
                            workspace.Id,
                            workspace.Name,
                            workspace.Description,
                            workspace.SemanticVersion,
                            CurrentPhaseName = workspace.CurrentPhase?.Name,
                            CurrentPhaseColor = workspace.CurrentPhase?.Color,
                            workspace.IsArchived,
                            workspace.CreatedAt,
                            workspace.UpdatedAt,
                            OwnerName = workspace.Owner.UserName
                        };

                        await cacheService.SetAsync(summaryKey, summary, CacheConfiguration.WorkspaceCacheTTL);
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogDebug("Workspaces warming concluído: {Count} workspaces", recentWorkspaces.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no warmup de workspaces");
            }
        }
    }
}
```

## 5. Configuration

### 5.1 Program.cs Configuration

#### Program.cs (adições)
```csharp
using IDE.Infrastructure.Cache;

// Redis Configuration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "IDE_Workspace";
});

// Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(configuration);
});

// Cache Services
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
builder.Services.AddHostedService<CacheWarmupService>();
```

### 5.2 appsettings.json

#### appsettings.json (adições)
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "CacheSettings": {
    "DefaultTTLMinutes": 30,
    "WorkspaceTTLMinutes": 30,
    "ItemsTTLMinutes": 15,
    "PermissionsTTLMinutes": 60,
    "NavigationStateTTLMinutes": 30,
    "SystemParametersTTLHours": 24,
    "MaxItemsPerList": 1000,
    "EnableCacheWarmup": true,
    "WarmupIntervalHours": 6
  }
}
```

## 6. Próximos Passos

**Parte 9**: SignalR Hub Básico
- WorkspaceHub implementation
- Real-time notifications
- User groups management
- Basic synchronization

**Validação desta Parte**:
- [ ] Redis está conectado e funcionando
- [ ] Cache service funciona corretamente
- [ ] Invalidação funciona conforme esperado
- [ ] Cache warmup está executando
- [ ] Performance melhorada em queries frequentes

## 7. Características Implementadas

✅ **RedisCacheService completo** com todas as operações  
✅ **Cache key patterns** organizados e consistentes  
✅ **TTL configurations** otimizados por tipo de dados  
✅ **Cache invalidation** granular e eficiente  
✅ **Cache warmup** automático para performance  
✅ **Error handling** robusto com fallback  
✅ **Logging detalhado** para debugging  
✅ **Batch operations** para performance  

## 8. Notas Importantes

⚠️ **Redis server** deve estar configurado e executando  
⚠️ **Memory limits** devem ser configurados no Redis  
⚠️ **Cache invalidation** deve ser chamada em todas as operações de escrita  
⚠️ **TTL values** podem precisar ajustes baseados no uso  
⚠️ **Cache warmup** pode impactar performance inicial  
⚠️ **Monitoring** do Redis é recomendado em produção  

Esta parte estabelece um **sistema de cache robusto e performático** que irá acelerar significativamente as operações do workspace. A próxima parte implementará SignalR para sincronização em tempo real.