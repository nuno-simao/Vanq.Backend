# Fase 4 - Parte 5: Database & API Optimization

## Contexto da Implementação

Esta é a **quinta parte da Fase 4** focada na **otimização completa de banco de dados PostgreSQL** e **APIs REST** com caching headers, compressão e métricas de performance.

### Objetivos da Parte 5
✅ **PostgreSQL** optimization estratégica  
✅ **Database health monitoring** completo  
✅ **API performance middleware** avançado  
✅ **Response compression** otimizada  
✅ **Query optimization** automática  
✅ **Connection pooling** eficiente  

### Pré-requisitos
- Partes 1-4 implementadas e funcionais
- PostgreSQL Azure/Local configurado
- Redis cache funcionando
- ApplicationDbContext implementado

---

## 3.2 Database Optimization

Configuração otimizada do PostgreSQL para performance, conexões e indexação estratégica.

### IDE.Infrastructure/Data/ApplicationDbContext.cs (Performance Optimizations Extensions)
```csharp
// ApplicationDbContext já definido no arquivo 04-01-frontend-service-integration.md
// Esta seção adiciona otimizações de performance ao ApplicationDbContext existente

public partial class ApplicationDbContext
{
    // Extensões de performance para o ApplicationDbContext

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            return;

        // Performance optimizations
        optionsBuilder.EnableSensitiveDataLogging(false);
        optionsBuilder.EnableServiceProviderCaching();
        optionsBuilder.EnableDetailedErrors(false);
        
        // Connection pooling configuration
        optionsBuilder.UseNpgsql(options =>
        {
            options.CommandTimeout(30);
            options.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Strategic indexing for performance
        ConfigureWorkspaceIndexes(modelBuilder);
        ConfigureModuleItemIndexes(modelBuilder);
        ConfigureChatIndexes(modelBuilder);
        ConfigureCollaborationIndexes(modelBuilder);
        ConfigurePermissionIndexes(modelBuilder);
        ConfigureRelationships(modelBuilder);
    }

    private void ConfigureWorkspaceIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>()
            .HasIndex(w => w.CreatedBy)
            .HasDatabaseName("IX_Workspaces_CreatedBy");

        modelBuilder.Entity<Workspace>()
            .HasIndex(w => new { w.IsArchived, w.CreatedAt })
            .HasDatabaseName("IX_Workspaces_IsArchived_CreatedAt");

        modelBuilder.Entity<Workspace>()
            .HasIndex(w => new { w.CreatedBy, w.IsArchived })
            .HasDatabaseName("IX_Workspaces_CreatedBy_IsArchived");

        modelBuilder.Entity<Workspace>()
            .HasIndex(w => w.LastModified)
            .HasDatabaseName("IX_Workspaces_LastModified");
    }

    private void ConfigureModuleItemIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => m.WorkspaceId)
            .HasDatabaseName("IX_ModuleItems_WorkspaceId");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => new { m.WorkspaceId, m.Module })
            .HasDatabaseName("IX_ModuleItems_WorkspaceId_Module");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => m.ParentId)
            .HasDatabaseName("IX_ModuleItems_ParentId");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => new { m.WorkspaceId, m.ParentId, m.Order })
            .HasDatabaseName("IX_ModuleItems_WorkspaceId_ParentId_Order");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => new { m.WorkspaceId, m.IsArchived })
            .HasDatabaseName("IX_ModuleItems_WorkspaceId_IsArchived");
    }

    private void ConfigureChatIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(c => new { c.WorkspaceId, c.CreatedAt })
            .HasDatabaseName("IX_ChatMessages_WorkspaceId_CreatedAt");

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(c => new { c.UserId, c.CreatedAt })
            .HasDatabaseName("IX_ChatMessages_UserId_CreatedAt");

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(c => c.ParentMessageId)
            .HasDatabaseName("IX_ChatMessages_ParentMessageId");

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(c => new { c.WorkspaceId, c.ParentMessageId })
            .HasDatabaseName("IX_ChatMessages_WorkspaceId_ParentMessageId");
    }

    private void ConfigureCollaborationIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollaborationSession>()
            .HasIndex(cs => new { cs.WorkspaceId, cs.IsActive })
            .HasDatabaseName("IX_CollaborationSessions_WorkspaceId_IsActive");

        modelBuilder.Entity<CollaborationSession>()
            .HasIndex(cs => new { cs.UserId, cs.IsActive })
            .HasDatabaseName("IX_CollaborationSessions_UserId_IsActive");

        modelBuilder.Entity<CollaborationSession>()
            .HasIndex(cs => cs.LastActivity)
            .HasDatabaseName("IX_CollaborationSessions_LastActivity");

        modelBuilder.Entity<ItemChangeRecord>()
            .HasIndex(icr => new { icr.ItemId, icr.Timestamp })
            .HasDatabaseName("IX_ItemChangeRecords_ItemId_Timestamp");

        modelBuilder.Entity<ItemChangeRecord>()
            .HasIndex(icr => new { icr.UserId, icr.Timestamp })
            .HasDatabaseName("IX_ItemChangeRecords_UserId_Timestamp");
    }

    private void ConfigurePermissionIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkspacePermission>()
            .HasIndex(wp => new { wp.UserId, wp.WorkspaceId })
            .IsUnique()
            .HasDatabaseName("IX_WorkspacePermissions_UserId_WorkspaceId");

        modelBuilder.Entity<WorkspacePermission>()
            .HasIndex(wp => wp.WorkspaceId)
            .HasDatabaseName("IX_WorkspacePermissions_WorkspaceId");
    }

    private void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // Module items hierarchy
        modelBuilder.Entity<ModuleItem>()
            .HasOne(m => m.Parent)
            .WithMany(m => m.Children)
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chat message replies
        modelBuilder.Entity<ChatMessage>()
            .HasOne(c => c.ParentMessage)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure cascade deletes carefully
        modelBuilder.Entity<Workspace>()
            .HasMany(w => w.ModuleItems)
            .WithOne(m => m.Workspace)
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Workspace>()
            .HasMany(w => w.ChatMessages)
            .WithOne(c => c.Workspace)
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Database Optimization Service

#### IDE.Infrastructure/Data/IDatabaseOptimizationService.cs
```csharp
public interface IDatabaseOptimizationService
{
    Task OptimizeQueriesAsync();
    Task UpdateStatisticsAsync();
    Task VacuumAnalyzeAsync();
    Task<DatabaseHealthInfo> GetHealthInfoAsync();
    Task<List<SlowQueryInfo>> GetSlowQueriesAsync();
    Task OptimizeIndexesAsync();
}

public class DatabaseOptimizationService : IDatabaseOptimizationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseOptimizationService> _logger;
    private readonly string _connectionString;

    public DatabaseOptimizationService(
        ApplicationDbContext context, 
        ILogger<DatabaseOptimizationService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task OptimizeQueriesAsync()
    {
        try
        {
            // Enable pg_stat_statements if not enabled
            await _context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_stat_statements;");

            // Reset statistics for fresh analysis
            await _context.Database.ExecuteSqlRawAsync("SELECT pg_stat_statements_reset();");

            _logger.LogInformation("Database query optimization initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during query optimization setup");
        }
    }

    public async Task UpdateStatisticsAsync()
    {
        try
        {
            // Update table statistics for query planner
            await _context.Database.ExecuteSqlRawAsync("ANALYZE;");
            
            _logger.LogInformation("Database statistics updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating database statistics");
        }
    }

    public async Task VacuumAnalyzeAsync()
    {
        try
        {
            // Full vacuum and analyze (use carefully in production)
            var tables = new[] { "Workspaces", "ModuleItems", "ChatMessages", "CollaborationSessions", "ItemChangeRecords" };
            
            foreach (var table in tables)
            {
                await _context.Database.ExecuteSqlRawAsync($"VACUUM ANALYZE \"{table}\";");
                _logger.LogDebug("Vacuumed and analyzed table {Table}", table);
            }

            _logger.LogInformation("Database vacuum and analyze completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during vacuum analyze");
        }
    }

    public async Task<DatabaseHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var healthInfo = new DatabaseHealthInfo
            {
                IsHealthy = true,
                ConnectionCount = await GetActiveConnectionsAsync(connection),
                DatabaseSize = await GetDatabaseSizeAsync(connection),
                LargestTables = await GetLargestTablesAsync(connection),
                IndexUsage = await GetIndexUsageAsync(connection),
                CacheHitRatio = await GetCacheHitRatioAsync(connection),
                DeadlockCount = await GetDeadlockCountAsync(connection),
                CheckedAt = DateTime.UtcNow
            };

            return healthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database health info");
            return new DatabaseHealthInfo
            {
                IsHealthy = false,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<List<SlowQueryInfo>> GetSlowQueriesAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var slowQueries = new List<SlowQueryInfo>();
            
            using var command = new NpgsqlCommand(@"
                SELECT 
                    query,
                    calls,
                    total_time,
                    mean_time,
                    stddev_time,
                    rows,
                    100.0 * shared_blks_hit / nullif(shared_blks_hit + shared_blks_read, 0) AS hit_percent
                FROM pg_stat_statements 
                WHERE calls > 10 AND mean_time > 50
                ORDER BY mean_time DESC 
                LIMIT 20", connection);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                slowQueries.Add(new SlowQueryInfo
                {
                    Query = reader.GetString("query"),
                    Calls = reader.GetInt64("calls"),
                    TotalTime = reader.GetDouble("total_time"),
                    MeanTime = reader.GetDouble("mean_time"),
                    StdDevTime = reader.IsDBNull("stddev_time") ? 0 : reader.GetDouble("stddev_time"),
                    Rows = reader.GetInt64("rows"),
                    HitPercent = reader.IsDBNull("hit_percent") ? 0 : reader.GetDouble("hit_percent")
                });
            }

            return slowQueries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slow queries");
            return new List<SlowQueryInfo>();
        }
    }

    public async Task OptimizeIndexesAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check for unused indexes
            var unusedIndexes = await GetUnusedIndexesAsync(connection);
            
            foreach (var index in unusedIndexes.Take(5)) // Be conservative
            {
                _logger.LogWarning("Found unused index: {IndexName} on table {TableName}", 
                    index.IndexName, index.TableName);
            }

            // Check for missing indexes on foreign keys
            await CheckMissingIndexesAsync(connection);

            _logger.LogInformation("Index optimization analysis completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during index optimization");
        }
    }

    private async Task<int> GetActiveConnectionsAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE state = 'active'", 
            connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<long> GetDatabaseSizeAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            "SELECT pg_database_size(current_database())", 
            connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private async Task<List<TableInfo>> GetLargestTablesAsync(NpgsqlConnection connection)
    {
        var tables = new List<TableInfo>();
        using var command = new NpgsqlCommand(@"
            SELECT 
                schemaname, 
                tablename, 
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
                pg_total_relation_size(schemaname||'.'||tablename) as size_bytes,
                n_tup_ins + n_tup_upd + n_tup_del as total_writes,
                seq_scan + idx_scan as total_reads
            FROM pg_tables t
            LEFT JOIN pg_stat_user_tables s ON t.tablename = s.relname
            WHERE schemaname = 'public'
            ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC 
            LIMIT 10", connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Name = reader.GetString("tablename"),
                Size = reader.GetString("size"),
                SizeBytes = reader.GetInt64("size_bytes"),
                TotalWrites = reader.IsDBNull("total_writes") ? 0 : reader.GetInt64("total_writes"),
                TotalReads = reader.IsDBNull("total_reads") ? 0 : reader.GetInt64("total_reads")
            });
        }

        return tables;
    }

    private async Task<decimal> GetIndexUsageAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(@"
            SELECT ROUND(
                CASE 
                    WHEN SUM(seq_scan) + SUM(idx_scan) = 0 THEN 0
                    ELSE 100.0 * SUM(idx_scan) / (SUM(seq_scan) + SUM(idx_scan))
                END, 2
            ) as index_usage_percentage
            FROM pg_stat_user_tables", connection);

        var result = await command.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
    }

    private async Task<decimal> GetCacheHitRatioAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(@"
            SELECT ROUND(
                100.0 * sum(blks_hit) / (sum(blks_hit) + sum(blks_read) + 0.001), 2
            ) as cache_hit_ratio
            FROM pg_stat_database 
            WHERE datname = current_database()", connection);

        var result = await command.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
    }

    private async Task<long> GetDeadlockCountAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(
            "SELECT deadlocks FROM pg_stat_database WHERE datname = current_database()", 
            connection);
        var result = await command.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }

    private async Task<List<IndexInfo>> GetUnusedIndexesAsync(NpgsqlConnection connection)
    {
        var indexes = new List<IndexInfo>();
        using var command = new NpgsqlCommand(@"
            SELECT 
                schemaname,
                tablename,
                indexname,
                idx_scan,
                pg_size_pretty(pg_relation_size(indexname::regclass)) as index_size
            FROM pg_stat_user_indexes
            WHERE idx_scan = 0
            AND indexname NOT LIKE '%_pkey'
            ORDER BY pg_relation_size(indexname::regclass) DESC", connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(new IndexInfo
            {
                TableName = reader.GetString("tablename"),
                IndexName = reader.GetString("indexname"),
                ScanCount = reader.GetInt64("idx_scan"),
                Size = reader.GetString("index_size")
            });
        }

        return indexes;
    }

    private async Task CheckMissingIndexesAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(@"
            SELECT 
                conrelid::regclass as table_name,
                conname as constraint_name,
                pg_get_constraintdef(c.oid) as constraint_def
            FROM pg_constraint c
            JOIN pg_class t ON c.conrelid = t.oid
            JOIN pg_namespace n ON t.relnamespace = n.oid
            WHERE c.contype = 'f'
            AND n.nspname = 'public'
            AND NOT EXISTS (
                SELECT 1 FROM pg_index i
                WHERE i.indrelid = c.conrelid
                AND i.indkey[0] = c.conkey[1]
            )", connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            _logger.LogWarning("Missing index on foreign key: {TableName}.{ConstraintName}",
                reader.GetString("table_name"), reader.GetString("constraint_name"));
        }
    }
}
```

### Database Models para Health Monitoring

#### IDE.Domain/Infrastructure/DatabaseHealthInfo.cs
```csharp
public class DatabaseHealthInfo
{
    public bool IsHealthy { get; set; }
    public int ConnectionCount { get; set; }
    public long DatabaseSize { get; set; }
    public List<TableInfo> LargestTables { get; set; } = new();
    public decimal IndexUsage { get; set; }
    public decimal CacheHitRatio { get; set; }
    public long DeadlockCount { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class TableInfo
{
    public string Name { get; set; }
    public string Size { get; set; }
    public long SizeBytes { get; set; }
    public long TotalWrites { get; set; }
    public long TotalReads { get; set; }
}

public class SlowQueryInfo
{
    public string Query { get; set; }
    public long Calls { get; set; }
    public double TotalTime { get; set; }
    public double MeanTime { get; set; }
    public double StdDevTime { get; set; }
    public long Rows { get; set; }
    public double HitPercent { get; set; }
}

public class IndexInfo
{
    public string TableName { get; set; }
    public string IndexName { get; set; }
    public long ScanCount { get; set; }
    public string Size { get; set; }
}
```

---

## 3.3 API Performance Optimization

Configurações avançadas de performance para APIs REST com middleware, compressão e métricas.

### Performance Middleware

#### IDE.API/Middleware/PerformanceMiddleware.cs
```csharp
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IPerformanceMetrics _metrics;

    public PerformanceMiddleware(
        RequestDelegate next, 
        ILogger<PerformanceMiddleware> logger, 
        IPerformanceMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = GetEndpointName(context);
        var requestSize = GetRequestSize(context);

        try
        {
            // Add performance headers
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Add("X-Response-Time", stopwatch.ElapsedMilliseconds.ToString());
                context.Response.Headers.Add("X-Server-Name", Environment.MachineName);
                context.Response.Headers.Add("X-Request-Id", context.TraceIdentifier);
                return Task.CompletedTask;
            });

            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            var responseSize = GetResponseSize(context);
            
            // Record comprehensive metrics
            _metrics.RecordApiCall(new ApiCallMetrics
            {
                Endpoint = endpoint,
                Method = context.Request.Method,
                StatusCode = context.Response.StatusCode,
                Duration = stopwatch.ElapsedMilliseconds,
                RequestSize = requestSize,
                ResponseSize = responseSize,
                Timestamp = DateTime.UtcNow,
                UserId = GetUserId(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                IpAddress = GetClientIpAddress(context)
            });

            // Log performance issues
            if (stopwatch.ElapsedMilliseconds > 2000)
            {
                _logger.LogWarning("Very slow request: {Endpoint} took {ElapsedMs}ms - Status: {StatusCode} - User: {UserId}",
                    endpoint, stopwatch.ElapsedMilliseconds, context.Response.StatusCode, GetUserId(context));
            }
            else if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogInformation("Slow request: {Endpoint} took {ElapsedMs}ms - Status: {StatusCode}",
                    endpoint, stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
            }
        }
    }

    private string GetEndpointName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<RouteAttribute>() != null)
        {
            var routeAttribute = endpoint.Metadata.GetMetadata<RouteAttribute>();
            return $"{context.Request.Method} /{routeAttribute.Template}";
        }
        
        return endpoint?.DisplayName ?? $"{context.Request.Method} {context.Request.Path}";
    }

    private long GetRequestSize(HttpContext context)
    {
        return context.Request.ContentLength ?? 0;
    }

    private long GetResponseSize(HttpContext context)
    {
        if (context.Response.Headers.TryGetValue("Content-Length", out var contentLength))
        {
            if (long.TryParse(contentLength.FirstOrDefault(), out var size))
            {
                return size;
            }
        }
        return 0;
    }

    private string GetUserId(HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            return xForwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
```

### Performance Metrics Service

#### IDE.Infrastructure/Metrics/IPerformanceMetrics.cs
```csharp
public interface IPerformanceMetrics
{
    void RecordApiCall(ApiCallMetrics metrics);
    Task<ApiPerformanceReport> GetPerformanceReportAsync(DateTime from, DateTime to);
    Task<List<EndpointMetrics>> GetEndpointMetricsAsync();
    Task<SystemMetrics> GetSystemMetricsAsync();
}

public class PerformanceMetrics : IPerformanceMetrics
{
    private readonly IRedisCacheService _cache;
    private readonly ILogger<PerformanceMetrics> _logger;
    private readonly ConcurrentDictionary<string, EndpointStats> _endpointStats;
    private readonly Timer _flushTimer;

    public PerformanceMetrics(IRedisCacheService cache, ILogger<PerformanceMetrics> logger)
    {
        _cache = cache;
        _logger = logger;
        _endpointStats = new ConcurrentDictionary<string, EndpointStats>();
        
        // Flush metrics to Redis every minute
        _flushTimer = new Timer(FlushMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void RecordApiCall(ApiCallMetrics metrics)
    {
        var key = $"{metrics.Method}:{metrics.Endpoint}";
        
        _endpointStats.AddOrUpdate(key, 
            new EndpointStats
            {
                Endpoint = metrics.Endpoint,
                Method = metrics.Method,
                TotalCalls = 1,
                TotalDuration = metrics.Duration,
                MinDuration = metrics.Duration,
                MaxDuration = metrics.Duration,
                ErrorCount = metrics.StatusCode >= 400 ? 1 : 0,
                LastCall = metrics.Timestamp
            },
            (k, existing) =>
            {
                existing.TotalCalls++;
                existing.TotalDuration += metrics.Duration;
                existing.MinDuration = Math.Min(existing.MinDuration, metrics.Duration);
                existing.MaxDuration = Math.Max(existing.MaxDuration, metrics.Duration);
                existing.ErrorCount += metrics.StatusCode >= 400 ? 1 : 0;
                existing.LastCall = metrics.Timestamp;
                return existing;
            });
    }

    public async Task<ApiPerformanceReport> GetPerformanceReportAsync(DateTime from, DateTime to)
    {
        try
        {
            var report = new ApiPerformanceReport
            {
                From = from,
                To = to,
                GeneratedAt = DateTime.UtcNow,
                Endpoints = await GetEndpointMetricsAsync(),
                SystemMetrics = await GetSystemMetricsAsync()
            };

            // Calculate aggregated metrics
            report.TotalRequests = report.Endpoints.Sum(e => e.TotalCalls);
            report.AverageResponseTime = report.Endpoints.Any() 
                ? report.Endpoints.Average(e => e.AverageDuration) 
                : 0;
            report.ErrorRate = report.TotalRequests > 0 
                ? (double)report.Endpoints.Sum(e => e.ErrorCount) / report.TotalRequests * 100 
                : 0;

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
            return new ApiPerformanceReport { From = from, To = to, GeneratedAt = DateTime.UtcNow };
        }
    }

    public async Task<List<EndpointMetrics>> GetEndpointMetricsAsync()
    {
        var metrics = new List<EndpointMetrics>();

        foreach (var stat in _endpointStats.Values)
        {
            metrics.Add(new EndpointMetrics
            {
                Endpoint = stat.Endpoint,
                Method = stat.Method,
                TotalCalls = stat.TotalCalls,
                AverageDuration = stat.TotalCalls > 0 ? (double)stat.TotalDuration / stat.TotalCalls : 0,
                MinDuration = stat.MinDuration,
                MaxDuration = stat.MaxDuration,
                ErrorCount = stat.ErrorCount,
                ErrorRate = stat.TotalCalls > 0 ? (double)stat.ErrorCount / stat.TotalCalls * 100 : 0,
                LastCall = stat.LastCall
            });
        }

        return metrics.OrderByDescending(m => m.TotalCalls).ToList();
    }

    public async Task<SystemMetrics> GetSystemMetricsAsync()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            return new SystemMetrics
            {
                CpuUsage = await GetCpuUsageAsync(),
                MemoryUsage = process.WorkingSet64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                GcCollections = new[]
                {
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2)
                },
                TotalMemory = GC.GetTotalMemory(false),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system metrics");
            return new SystemMetrics { Timestamp = DateTime.UtcNow };
        }
    }

    private async void FlushMetrics(object state)
    {
        try
        {
            foreach (var kvp in _endpointStats.ToList())
            {
                var key = $"metrics:endpoint:{kvp.Key}";
                await _cache.SetAsync(key, kvp.Value, TimeSpan.FromHours(24));
            }

            _logger.LogDebug("Flushed {Count} endpoint metrics to cache", _endpointStats.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing metrics to cache");
        }
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            await Task.Delay(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return cpuUsageTotal * 100;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
    }
}
```

### Response Compression Configuration

#### IDE.API/Configuration/CompressionConfiguration.cs
```csharp
public static class CompressionConfiguration
{
    public static IServiceCollection AddResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            
            // Add compression providers in order of preference
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            
            // MIME types to compress
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/javascript",
                "application/xml",
                "text/css",
                "text/html",
                "text/json",
                "text/plain",
                "text/xml",
                "application/atom+xml",
                "application/rss+xml",
                "application/xhtml+xml",
                "image/svg+xml",
                "application/font-woff",
                "application/font-woff2"
            });
        });

        // Configure Brotli compression
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        // Configure Gzip compression
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }
}
```

### Metrics Models

#### IDE.Domain/Infrastructure/PerformanceMetrics.cs
```csharp
public class ApiCallMetrics
{
    public string Endpoint { get; set; }
    public string Method { get; set; }
    public int StatusCode { get; set; }
    public long Duration { get; set; }
    public long RequestSize { get; set; }
    public long ResponseSize { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public string UserAgent { get; set; }
    public string IpAddress { get; set; }
}

public class EndpointStats
{
    public string Endpoint { get; set; }
    public string Method { get; set; }
    public long TotalCalls { get; set; }
    public long TotalDuration { get; set; }
    public long MinDuration { get; set; } = long.MaxValue;
    public long MaxDuration { get; set; }
    public long ErrorCount { get; set; }
    public DateTime LastCall { get; set; }
}

public class EndpointMetrics
{
    public string Endpoint { get; set; }
    public string Method { get; set; }
    public long TotalCalls { get; set; }
    public double AverageDuration { get; set; }
    public long MinDuration { get; set; }
    public long MaxDuration { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate { get; set; }
    public DateTime LastCall { get; set; }
}

public class ApiPerformanceReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime GeneratedAt { get; set; }
    public long TotalRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public List<EndpointMetrics> Endpoints { get; set; } = new();
    public SystemMetrics SystemMetrics { get; set; }
}

public class SystemMetrics
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int[] GcCollections { get; set; }
    public long TotalMemory { get; set; }
    public DateTime Timestamp { get; set; }
}
```

---

## Entregáveis da Parte 5

### ✅ Implementações Completas
- **ApplicationDbContext** com indexação estratégica
- **DatabaseOptimizationService** com análise completa
- **PerformanceMiddleware** com métricas detalhadas
- **PerformanceMetrics** service com Redis storage
- **ResponseCompression** otimizada
- **Database health monitoring** completo

### ✅ Funcionalidades de Otimização
- **Strategic indexing** para queries principais
- **Query optimization** com pg_stat_statements
- **Connection pooling** eficiente
- **Performance monitoring** em tempo real
- **Slow query detection** automática
- **Compression** Brotli/Gzip otimizada

### ✅ Métricas e Monitoramento
- **API response times** por endpoint
- **Database health** indicators
- **System resource** usage
- **Error rate** tracking
- **Cache hit ratios** monitoring
- **Index usage** analysis

---

## Validação da Parte 5

### Critérios de Sucesso
- [ ] Database queries respondem < 100ms (p95)
- [ ] API endpoints respondem < 200ms (p95)  
- [ ] Index usage > 90% para queries principais
- [ ] Cache hit ratio > 85%
- [ ] Compression ratio > 60% para JSON responses
- [ ] Error rate < 1% para endpoints principais
- [ ] Database connections < 50 concurrent

### Testes de Performance
```bash
# 1. Database health check
curl -X GET http://localhost:8503/api/admin/database/health \
  -H "Authorization: Bearer <token>"

# 2. Performance metrics
curl -X GET http://localhost:8503/api/admin/performance/metrics \
  -H "Authorization: Bearer <token>"

# 3. Slow queries analysis
curl -X GET http://localhost:8503/api/admin/database/slow-queries \
  -H "Authorization: Bearer <token>"
```

### Performance Targets
- **Database queries**: < 50ms (avg)
- **API responses**: < 150ms (avg)
- **Memory usage**: < 512MB steady state
- **CPU usage**: < 30% average
- **Response compression**: > 70% reduction

---

## Próximos Passos

Após validação da Parte 5, prosseguir para:
- **Parte 6**: Rate Limiting & Security Enhancement

---

**Tempo Estimado**: 3-4 horas  
**Complexidade**: Alta  
**Dependências**: PostgreSQL, Redis, ApplicationDbContext  
**Entregável**: Sistema de banco e APIs otimizados com monitoramento completo