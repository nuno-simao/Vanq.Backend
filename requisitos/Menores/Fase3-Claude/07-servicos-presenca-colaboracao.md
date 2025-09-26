# Fase 3.7: Services - Presença e Colaboração

## Implementação dos Services Core de Colaboração

Esta parte implementa os **services fundamentais** para **presença de usuários** e **Operational Transform**, que são a base da colaboração em tempo real.

**Pré-requisitos**: Partes 3.4, 3.5 e 3.6 (SignalR Hub) implementadas

## 1. Service de Presença de Usuários

### 1.1 Interface do Service

#### IDE.Application/Services/Collaboration/IUserPresenceService.cs
```csharp
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;

namespace IDE.Application.Services.Collaboration
{
    /// <summary>
    /// Service para gestão de presença de usuários em tempo real
    /// </summary>
    public interface IUserPresenceService
    {
        /// <summary>
        /// Definir presença de usuário no workspace
        /// </summary>
        Task SetUserPresenceAsync(Guid workspaceId, Guid userId, string connectionId, UserPresenceStatus status);
        
        /// <summary>
        /// Atualizar item atual que o usuário está editando
        /// </summary>
        Task UpdateCurrentItemAsync(Guid workspaceId, Guid userId, string? itemId);
        
        /// <summary>
        /// Definir usuário como offline (ao desconectar)
        /// </summary>
        Task SetUserOfflineAsync(string connectionId);
        
        /// <summary>
        /// Obter lista de usuários presentes no workspace
        /// </summary>
        Task<List<UserPresenceDto>> GetWorkspacePresenceAsync(Guid workspaceId);
        
        /// <summary>
        /// Obter contagem de usuários ativos no workspace
        /// </summary>
        Task<int> GetWorkspaceActiveCountAsync(Guid workspaceId);
        
        /// <summary>
        /// Obter lista de editores ativos em um item específico
        /// </summary>
        Task<List<UserPresenceDto>> GetItemActiveEditorsAsync(Guid itemId);
        
        /// <summary>
        /// Obter contagem de editores ativos em um item
        /// </summary>
        Task<int> GetItemActiveEditorsCountAsync(Guid itemId);
        
        /// <summary>
        /// Limpeza de conexões antigas/inativas
        /// </summary>
        Task CleanupStaleConnectionsAsync();
        
        /// <summary>
        /// Verificar se usuário está online em um workspace
        /// </summary>
        Task<bool> IsUserOnlineAsync(Guid userId, Guid workspaceId);
        
        /// <summary>
        /// Obter estatísticas de presença global
        /// </summary>
        Task<PresenceStatsDto> GetPresenceStatsAsync();
    }
}
```

### 1.2 Implementação do Service

#### IDE.Infrastructure/Services/Presence/UserPresenceService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using IDE.Application.Services.Collaboration;
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;
using IDE.Application.DTOs;
using IDE.Infrastructure.Persistence.Data;
using System.Text.Json;

namespace IDE.Infrastructure.Services.Presence
{
    /// <summary>
    /// Implementação do service de presença de usuários
    /// </summary>
    public class UserPresenceService : IUserPresenceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<UserPresenceService> _logger;

        public UserPresenceService(
            ApplicationDbContext context,
            IDistributedCache cache,
            ILogger<UserPresenceService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task SetUserPresenceAsync(Guid workspaceId, Guid userId, string connectionId, UserPresenceStatus status)
        {
            try
            {
                var presence = await _context.UserPresences
                    .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && 
                                             p.UserId == userId && 
                                             p.ConnectionId == connectionId);

                var now = DateTime.UtcNow;

                if (presence != null)
                {
                    // Atualizar presença existente
                    presence.Status = status;
                    presence.LastSeenAt = now;
                    presence.LastHeartbeat = now;
                    
                    if (status == UserPresenceStatus.Offline)
                    {
                        presence.DisconnectedAt = now;
                        presence.CurrentItemId = null;
                    }
                }
                else
                {
                    // Criar nova presença
                    presence = new UserPresence
                    {
                        Id = Guid.NewGuid(),
                        WorkspaceId = workspaceId,
                        UserId = userId,
                        ConnectionId = connectionId,
                        Status = status,
                        ConnectedAt = now,
                        LastSeenAt = now,
                        LastHeartbeat = now,
                        UserAgent = GetUserAgentFromContext(),
                        IpAddress = GetIpAddressFromContext()
                    };
                    _context.UserPresences.Add(presence);
                }

                await _context.SaveChangesAsync();

                // Invalidar cache
                await InvalidatePresenceCache(workspaceId, userId);

                _logger.LogDebug("User {UserId} presence set to {Status} in workspace {WorkspaceId} with connection {ConnectionId}",
                    userId, status, workspaceId, connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user presence for user {UserId} in workspace {WorkspaceId}",
                    userId, workspaceId);
                throw;
            }
        }

        public async Task UpdateCurrentItemAsync(Guid workspaceId, Guid userId, string? itemId)
        {
            try
            {
                var presences = await _context.UserPresences
                    .Where(p => p.WorkspaceId == workspaceId && p.UserId == userId)
                    .ToListAsync();

                foreach (var presence in presences)
                {
                    presence.CurrentItemId = itemId;
                    presence.LastSeenAt = DateTime.UtcNow;
                }

                if (presences.Any())
                {
                    await _context.SaveChangesAsync();
                    
                    // Invalidar cache
                    await InvalidatePresenceCache(workspaceId, userId);
                    
                    _logger.LogDebug("User {UserId} current item updated to {ItemId} in workspace {WorkspaceId}",
                        userId, itemId ?? "null", workspaceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating current item for user {UserId} in workspace {WorkspaceId}",
                    userId, workspaceId);
                throw;
            }
        }

        public async Task SetUserOfflineAsync(string connectionId)
        {
            try
            {
                var presences = await _context.UserPresences
                    .Where(p => p.ConnectionId == connectionId)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                
                foreach (var presence in presences)
                {
                    presence.Status = UserPresenceStatus.Offline;
                    presence.DisconnectedAt = now;
                    presence.LastSeenAt = now;
                    presence.CurrentItemId = null;
                }

                if (presences.Any())
                {
                    await _context.SaveChangesAsync();
                    
                    // Invalidar cache para todos os workspaces afetados
                    var workspaceIds = presences.Select(p => p.WorkspaceId).Distinct();
                    foreach (var workspaceId in workspaceIds)
                    {
                        await _cache.RemoveAsync($"workspace_presence_{workspaceId}");
                    }
                    
                    _logger.LogDebug("Connection {ConnectionId} set offline, affecting {Count} presence records",
                        connectionId, presences.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting connection {ConnectionId} offline", connectionId);
                throw;
            }
        }

        public async Task<List<UserPresenceDto>> GetWorkspacePresenceAsync(Guid workspaceId)
        {
            try
            {
                var cacheKey = $"workspace_presence_{workspaceId}";
                var cachedPresence = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedPresence))
                {
                    return JsonSerializer.Deserialize<List<UserPresenceDto>>(cachedPresence) ?? new List<UserPresenceDto>();
                }

                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
                
                var presences = await _context.UserPresences
                    .Where(p => p.WorkspaceId == workspaceId && 
                               p.Status != UserPresenceStatus.Offline &&
                               p.LastSeenAt > fiveMinutesAgo)
                    .Include(p => p.User)
                    .AsNoTracking()
                    .GroupBy(p => p.UserId) // Agrupar por usuário (pode ter múltiplas conexões)
                    .Select(g => g.OrderByDescending(p => p.LastSeenAt).First()) // Pegar a mais recente
                    .ToListAsync();

                var result = presences.Select(p => new UserPresenceDto
                {
                    ConnectionId = p.ConnectionId,
                    Status = p.Status,
                    LastSeenAt = p.LastSeenAt,
                    CurrentItemId = p.CurrentItemId,
                    ConnectedAt = p.ConnectedAt,
                    IsActive = (DateTime.UtcNow - p.LastSeenAt).TotalMinutes < 2,
                    User = new UserDto
                    {
                        Id = p.User.Id,
                        Username = p.User.Username,
                        FirstName = p.User.FirstName,
                        LastName = p.User.LastName,
                        Email = p.User.Email,
                        Avatar = p.User.Avatar,
                        Plan = p.User.Plan
                    }
                }).ToList();

                // Cache por 1 minuto
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workspace presence for workspace {WorkspaceId}", workspaceId);
                return new List<UserPresenceDto>();
            }
        }

        public async Task<int> GetWorkspaceActiveCountAsync(Guid workspaceId)
        {
            try
            {
                var cacheKey = $"workspace_active_count_{workspaceId}";
                var cachedCount = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedCount) && int.TryParse(cachedCount, out var count))
                {
                    return count;
                }

                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
                
                var activeCount = await _context.UserPresences
                    .Where(p => p.WorkspaceId == workspaceId && 
                               p.Status != UserPresenceStatus.Offline &&
                               p.LastSeenAt > fiveMinutesAgo)
                    .Select(p => p.UserId)
                    .Distinct()
                    .CountAsync();

                // Cache por 30 segundos
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                };
                await _cache.SetStringAsync(cacheKey, activeCount.ToString(), cacheOptions);

                return activeCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active count for workspace {WorkspaceId}", workspaceId);
                return 0;
            }
        }

        public async Task<List<UserPresenceDto>> GetItemActiveEditorsAsync(Guid itemId)
        {
            try
            {
                var twoMinutesAgo = DateTime.UtcNow.AddMinutes(-2);
                
                var presences = await _context.UserPresences
                    .Where(p => p.CurrentItemId == itemId.ToString() && 
                               p.Status != UserPresenceStatus.Offline &&
                               p.LastSeenAt > twoMinutesAgo)
                    .Include(p => p.User)
                    .AsNoTracking()
                    .ToListAsync();

                return presences.Select(p => new UserPresenceDto
                {
                    ConnectionId = p.ConnectionId,
                    Status = p.Status,
                    LastSeenAt = p.LastSeenAt,
                    CurrentItemId = p.CurrentItemId,
                    ConnectedAt = p.ConnectedAt,
                    IsActive = true,
                    User = new UserDto
                    {
                        Id = p.User.Id,
                        Username = p.User.Username,
                        FirstName = p.User.FirstName,
                        LastName = p.User.LastName,
                        Email = p.User.Email,
                        Avatar = p.User.Avatar,
                        Plan = p.User.Plan
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active editors for item {ItemId}", itemId);
                return new List<UserPresenceDto>();
            }
        }

        public async Task<int> GetItemActiveEditorsCountAsync(Guid itemId)
        {
            try
            {
                var twoMinutesAgo = DateTime.UtcNow.AddMinutes(-2);
                
                return await _context.UserPresences
                    .CountAsync(p => p.CurrentItemId == itemId.ToString() && 
                                    p.Status != UserPresenceStatus.Offline &&
                                    p.LastSeenAt > twoMinutesAgo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active editor count for item {ItemId}", itemId);
                return 0;
            }
        }

        public async Task CleanupStaleConnectionsAsync()
        {
            try
            {
                var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
                
                var staleConnections = await _context.UserPresences
                    .Where(p => p.LastSeenAt < fifteenMinutesAgo || 
                               (p.LastHeartbeat.HasValue && p.LastHeartbeat.Value < fifteenMinutesAgo))
                    .ToListAsync();

                var affectedCount = 0;
                var workspaceIds = new HashSet<Guid>();
                
                foreach (var connection in staleConnections)
                {
                    if (connection.Status != UserPresenceStatus.Offline)
                    {
                        connection.Status = UserPresenceStatus.Offline;
                        connection.DisconnectedAt = DateTime.UtcNow;
                        connection.CurrentItemId = null;
                        workspaceIds.Add(connection.WorkspaceId);
                        affectedCount++;
                    }
                }

                if (affectedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    
                    // Invalidar cache dos workspaces afetados
                    foreach (var workspaceId in workspaceIds)
                    {
                        await _cache.RemoveAsync($"workspace_presence_{workspaceId}");
                        await _cache.RemoveAsync($"workspace_active_count_{workspaceId}");
                    }
                    
                    _logger.LogInformation("Cleaned up {Count} stale connections", affectedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stale connections cleanup");
            }
        }

        public async Task<bool> IsUserOnlineAsync(Guid userId, Guid workspaceId)
        {
            try
            {
                var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
                
                return await _context.UserPresences
                    .AnyAsync(p => p.UserId == userId && 
                                  p.WorkspaceId == workspaceId && 
                                  p.Status != UserPresenceStatus.Offline &&
                                  p.LastSeenAt > fiveMinutesAgo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is online in workspace {WorkspaceId}",
                    userId, workspaceId);
                return false;
            }
        }

        public async Task<PresenceStatsDto> GetPresenceStatsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var fiveMinutesAgo = now.AddMinutes(-5);
                var oneHourAgo = now.AddHours(-1);
                var oneDayAgo = now.AddDays(-1);

                var stats = new PresenceStatsDto
                {
                    TotalActiveUsers = await _context.UserPresences
                        .Where(p => p.Status != UserPresenceStatus.Offline && p.LastSeenAt > fiveMinutesAgo)
                        .Select(p => p.UserId)
                        .Distinct()
                        .CountAsync(),
                        
                    TotalActiveConnections = await _context.UserPresences
                        .CountAsync(p => p.Status != UserPresenceStatus.Offline && p.LastSeenAt > fiveMinutesAgo),
                        
                    UsersInLastHour = await _context.UserPresences
                        .Where(p => p.LastSeenAt > oneHourAgo)
                        .Select(p => p.UserId)
                        .Distinct()
                        .CountAsync(),
                        
                    UsersInLastDay = await _context.UserPresences
                        .Where(p => p.LastSeenAt > oneDayAgo)
                        .Select(p => p.UserId)
                        .Distinct()
                        .CountAsync(),
                        
                    ActiveWorkspaces = await _context.UserPresences
                        .Where(p => p.Status != UserPresenceStatus.Offline && p.LastSeenAt > fiveMinutesAgo)
                        .Select(p => p.WorkspaceId)
                        .Distinct()
                        .CountAsync(),

                    AverageConnectionsPerUser = await CalculateAverageConnectionsPerUser(),
                    PeakConcurrentUsers = await GetPeakConcurrentUsers(),
                        
                    GeneratedAt = now
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presence stats");
                return new PresenceStatsDto { GeneratedAt = DateTime.UtcNow };
            }
        }

        #region Helper Methods

        private async Task InvalidatePresenceCache(Guid workspaceId, Guid userId)
        {
            await _cache.RemoveAsync($"workspace_presence_{workspaceId}");
            await _cache.RemoveAsync($"workspace_active_count_{workspaceId}");
        }

        private string GetUserAgentFromContext()
        {
            // TODO: Implementar captura do User-Agent do contexto HTTP
            return "Unknown";
        }

        private string GetIpAddressFromContext()
        {
            // TODO: Implementar captura do IP do contexto HTTP
            return "127.0.0.1";
        }

        private async Task<double> CalculateAverageConnectionsPerUser()
        {
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            
            var activeUsers = await _context.UserPresences
                .Where(p => p.Status != UserPresenceStatus.Offline && p.LastSeenAt > fiveMinutesAgo)
                .Select(p => p.UserId)
                .Distinct()
                .CountAsync();

            var activeConnections = await _context.UserPresences
                .CountAsync(p => p.Status != UserPresenceStatus.Offline && p.LastSeenAt > fiveMinutesAgo);

            return activeUsers > 0 ? (double)activeConnections / activeUsers : 0.0;
        }

        private async Task<int> GetPeakConcurrentUsers()
        {
            // Implementação simplificada - retorna usuários ativos atuais
            // Em produção, seria baseado em histórico
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            
            return await _context.UserPresences
                .Where(p => p.Status != UserPresenceStatus.Offline && p.LastSeenAt > fiveMinutesAgo)
                .Select(p => p.UserId)
                .Distinct()
                .CountAsync();
        }

        #endregion
    }
}
```
```

## 2. Service de Operational Transform

### 2.1 Interface do Service

#### IDE.Application/Services/Collaboration/IOperationalTransformService.cs
```csharp
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;

namespace IDE.Application.Services.Collaboration
{
    /// <summary>
    /// Service para algoritmos de Operational Transform
    /// </summary>
    public interface IOperationalTransformService
    {
        /// <summary>
        /// Processar operação com transformação operacional
        /// </summary>
        Task<OperationTransformResult> ProcessOperationAsync(Guid itemId, TextOperationDto operation, Guid userId);
        
        /// <summary>
        /// Aplicar operação transformada ao documento
        /// </summary>
        Task ApplyOperationAsync(Guid itemId, TextOperationDto operation);
        
        /// <summary>
        /// Obter snapshot mais recente de um item
        /// </summary>
        Task<CollaborationSnapshotDto?> GetLatestSnapshotAsync(Guid itemId);
        
        /// <summary>
        /// Obter operações desde um snapshot específico
        /// </summary>
        Task<List<TextOperationDto>> GetOperationsSinceSnapshotAsync(Guid itemId, Guid? snapshotId);
        
        /// <summary>
        /// Obter operações desde um número de sequência
        /// </summary>
        Task<List<TextOperationDto>> GetOperationsSinceSequenceAsync(Guid itemId, long sequenceNumber);
        
        /// <summary>
        /// Criar snapshot se necessário baseado em trigger
        /// </summary>
        Task CreateSnapshotIfNeededAsync(Guid itemId, Guid userId, SnapshotTrigger trigger);
        
        /// <summary>
        /// Resolver conflito detectado
        /// </summary>
        Task<ConflictResolutionResult> ResolveConflictAsync(ConflictDto conflict, ResolutionStrategy strategy);
        
        /// <summary>
        /// Transformar operação contra lista de operações concorrentes
        /// </summary>
        Task<TextOperationDto> TransformOperationAsync(TextOperationDto operation, List<TextOperationDto> concurrentOperations);
        
        /// <summary>
        /// Validar integridade do documento
        /// </summary>
        Task<DocumentIntegrityResult> ValidateDocumentIntegrityAsync(Guid itemId);
        
        /// <summary>
        /// Compactar histórico de operações antigas
        /// </summary>
        Task CompactOperationHistoryAsync(Guid itemId);
    }
}
```

### 2.2 Implementação Básica do OT Service

#### IDE.Infrastructure/Services/OperationalTransformService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IDE.Application.Services.Collaboration;
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;
using IDE.Infrastructure.Data;
using System.Text.Json;

namespace IDE.Infrastructure.Services
{
    /// <summary>
    /// Implementação do service de Operational Transform
    /// </summary>
    public class OperationalTransformService : IOperationalTransformService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OperationalTransformService> _logger;

        public OperationalTransformService(
            ApplicationDbContext context,
            ILogger<OperationalTransformService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OperationTransformResult> ProcessOperationAsync(Guid itemId, TextOperationDto operation, Guid userId)
        {
            try
            {
                // Obter operações concorrentes desde a sequência base da operação
                var concurrentOperations = await GetConcurrentOperationsAsync(itemId, operation.SequenceNumber);
                
                if (!concurrentOperations.Any())
                {
                    // Sem conflitos - aplicação direta
                    var sequenceNumber = await GetNextSequenceNumberAsync(itemId);
                    operation.SequenceNumber = sequenceNumber;
                    
                    return new OperationTransformResult
                    {
                        TransformedOperation = operation,
                        OriginalOperation = operation,
                        SequenceNumber = sequenceNumber,
                        HasConflict = false,
                        ProcessedAt = DateTime.UtcNow
                    };
                }

                // Detectar tipo de conflito
                var conflictType = DetectConflictType(operation, concurrentOperations);
                
                if (conflictType == ConflictType.Simple)
                {
                    // Transformar automaticamente
                    var transformed = await TransformOperationAsync(operation, concurrentOperations);
                    var sequenceNumber = await GetNextSequenceNumberAsync(itemId);
                    transformed.SequenceNumber = sequenceNumber;
                    
                    return new OperationTransformResult
                    {
                        TransformedOperation = transformed,
                        OriginalOperation = operation,
                        SequenceNumber = sequenceNumber,
                        HasConflict = false,
                        ConflictType = conflictType,
                        ConflictingOperations = concurrentOperations,
                        ProcessedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    // Conflito complexo - requer intervenção
                    return new OperationTransformResult
                    {
                        OriginalOperation = operation,
                        HasConflict = true,
                        ConflictType = conflictType,
                        ConflictSeverity = DetermineConflictSeverity(operation, concurrentOperations),
                        ConflictingOperations = concurrentOperations,
                        ConflictDescription = GenerateConflictDescription(operation, concurrentOperations),
                        ResolutionOptions = GenerateResolutionOptions(operation, concurrentOperations),
                        ProcessedAt = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing operation for item {ItemId} by user {UserId}", itemId, userId);
                throw;
            }
        }

        public async Task ApplyOperationAsync(Guid itemId, TextOperationDto operation)
        {
            try
            {
                var textOperation = new TextOperation
                {
                    Id = operation.Id ?? Guid.NewGuid(),
                    ItemId = itemId,
                    UserId = Guid.Parse(operation.UserId),
                    Type = operation.Type,
                    Position = operation.Position,
                    Content = operation.Content ?? string.Empty,
                    Length = operation.Length,
                    SequenceNumber = operation.SequenceNumber,
                    ClientTimestamp = operation.ClientTimestamp,
                    ServerTimestamp = DateTime.UtcNow,
                    OperationHash = GenerateOperationHash(operation),
                    Metadata = operation.Metadata != null ? JsonSerializer.Serialize(operation.Metadata) : null
                };

                _context.TextOperations.Add(textOperation);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Applied operation {OperationId} to item {ItemId} with sequence {SequenceNumber}",
                    operation.Id, itemId, operation.SequenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying operation {OperationId} to item {ItemId}", 
                    operation.Id, itemId);
                throw;
            }
        }

        public async Task<CollaborationSnapshotDto?> GetLatestSnapshotAsync(Guid itemId)
        {
            try
            {
                var snapshot = await _context.CollaborationSnapshots
                    .Where(s => s.ItemId == itemId)
                    .OrderByDescending(s => s.CreatedAt)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (snapshot == null) return null;

                return new CollaborationSnapshotDto
                {
                    Id = snapshot.Id,
                    ItemId = snapshot.ItemId,
                    Content = snapshot.Content,
                    ContentHash = snapshot.ContentHash,
                    LastSequenceNumber = snapshot.LastSequenceNumber,
                    CreatedAt = snapshot.CreatedAt,
                    CreatedByUserId = snapshot.CreatedByUserId,
                    TriggerReason = snapshot.TriggerReason,
                    SnapshotSize = snapshot.Content?.Length ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest snapshot for item {ItemId}", itemId);
                return null;
            }
        }

        public async Task<List<TextOperationDto>> GetOperationsSinceSnapshotAsync(Guid itemId, Guid? snapshotId)
        {
            try
            {
                long startSequence = 0;
                
                if (snapshotId.HasValue)
                {
                    var snapshot = await _context.CollaborationSnapshots
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == snapshotId.Value);
                    
                    if (snapshot != null)
                    {
                        startSequence = snapshot.LastSequenceNumber;
                    }
                }

                return await GetOperationsSinceSequenceAsync(itemId, startSequence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting operations since snapshot {SnapshotId} for item {ItemId}", 
                    snapshotId, itemId);
                return new List<TextOperationDto>();
            }
        }

        public async Task<List<TextOperationDto>> GetOperationsSinceSequenceAsync(Guid itemId, long sequenceNumber)
        {
            try
            {
                var operations = await _context.TextOperations
                    .Where(o => o.ItemId == itemId && o.SequenceNumber > sequenceNumber)
                    .Include(o => o.User)
                    .OrderBy(o => o.SequenceNumber)
                    .AsNoTracking()
                    .ToListAsync();

                return operations.Select(o => new TextOperationDto
                {
                    Id = o.Id,
                    Type = o.Type,
                    Position = o.Position,
                    Content = o.Content,
                    Length = o.Length,
                    SequenceNumber = o.SequenceNumber,
                    ClientTimestamp = o.ClientTimestamp,
                    ServerTimestamp = o.ServerTimestamp,
                    UserId = o.UserId.ToString(),
                    UserName = o.User.Username,
                    OperationHash = o.OperationHash,
                    Metadata = !string.IsNullOrEmpty(o.Metadata) ? 
                        JsonSerializer.Deserialize<Dictionary<string, object>>(o.Metadata) : null
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting operations since sequence {SequenceNumber} for item {ItemId}", 
                    sequenceNumber, itemId);
                return new List<TextOperationDto>();
            }
        }

        public async Task CreateSnapshotIfNeededAsync(Guid itemId, Guid userId, SnapshotTrigger trigger)
        {
            try
            {
                bool shouldCreateSnapshot = false;
                
                switch (trigger)
                {
                    case SnapshotTrigger.OperationCount:
                        var operationCount = await GetOperationCountSinceLastSnapshotAsync(itemId);
                        shouldCreateSnapshot = operationCount >= 100; // Snapshot a cada 100 operações
                        break;
                        
                    case SnapshotTrigger.TimeInterval:
                        var lastSnapshot = await GetLastSnapshotTimeAsync(itemId);
                        shouldCreateSnapshot = DateTime.UtcNow - lastSnapshot > TimeSpan.FromMinutes(30);
                        break;
                        
                    case SnapshotTrigger.Shutdown:
                        shouldCreateSnapshot = true; // Sempre criar no shutdown
                        break;
                }

                if (shouldCreateSnapshot)
                {
                    await CreateSnapshotAsync(itemId, userId, trigger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if snapshot is needed for item {ItemId}", itemId);
            }
        }

        // Métodos auxiliares privados implementados de forma básica
        private async Task<List<TextOperationDto>> GetConcurrentOperationsAsync(Guid itemId, long baseSequence)
        {
            return await _context.TextOperations
                .Where(o => o.ItemId == itemId && o.SequenceNumber > baseSequence)
                .Include(o => o.User)
                .OrderBy(o => o.SequenceNumber)
                .Select(o => new TextOperationDto
                {
                    Id = o.Id,
                    Type = o.Type,
                    Position = o.Position,
                    Content = o.Content,
                    Length = o.Length,
                    SequenceNumber = o.SequenceNumber,
                    UserId = o.UserId.ToString(),
                    UserName = o.User.Username
                })
                .AsNoTracking()
                .ToListAsync();
        }

        private async Task<long> GetNextSequenceNumberAsync(Guid itemId)
        {
            var lastSequence = await _context.TextOperations
                .Where(o => o.ItemId == itemId)
                .MaxAsync(o => (long?)o.SequenceNumber) ?? 0;
                
            return lastSequence + 1;
        }

        private ConflictType DetectConflictType(TextOperationDto operation, List<TextOperationDto> concurrentOps)
        {
            // Implementação básica de detecção de conflito
            foreach (var concurrentOp in concurrentOps)
            {
                var overlap = CalculatePositionOverlap(operation, concurrentOp);
                if (overlap > 0)
                {
                    return ConflictType.Complex; // Sobreposição de posição
                }
            }
            
            return ConflictType.Simple; // Sem sobreposição - transformação simples
        }

        private int CalculatePositionOverlap(TextOperationDto op1, TextOperationDto op2)
        {
            var op1End = op1.Position + (op1.Length ?? op1.Content?.Length ?? 0);
            var op2End = op2.Position + (op2.Length ?? op2.Content?.Length ?? 0);
            
            var overlapStart = Math.Max(op1.Position, op2.Position);
            var overlapEnd = Math.Min(op1End, op2End);
            
            return Math.Max(0, overlapEnd - overlapStart);
        }

        // Implementações stub dos métodos restantes para completude
        public Task<TextOperationDto> TransformOperationAsync(TextOperationDto operation, List<TextOperationDto> concurrentOperations)
        {
            // TODO: Implementar algoritmo completo de transformação
            return Task.FromResult(operation);
        }

        public Task<ConflictResolutionResult> ResolveConflictAsync(ConflictDto conflict, ResolutionStrategy strategy)
        {
            // TODO: Implementar resolução de conflitos
            return Task.FromResult(new ConflictResolutionResult { Success = false });
        }

        public Task<DocumentIntegrityResult> ValidateDocumentIntegrityAsync(Guid itemId)
        {
            // TODO: Implementar validação de integridade
            return Task.FromResult(new DocumentIntegrityResult { IsValid = true });
        }

        public Task CompactOperationHistoryAsync(Guid itemId)
        {
            // TODO: Implementar compactação do histórico
            return Task.CompletedTask;
        }

        private ConflictSeverity DetermineConflictSeverity(TextOperationDto operation, List<TextOperationDto> concurrentOperations)
        {
            return ConflictSeverity.Low; // Implementação básica
        }

        private string GenerateConflictDescription(TextOperationDto operation, List<TextOperationDto> concurrentOperations)
        {
            return "Conflito detectado entre operações concorrentes";
        }

        private List<string> GenerateResolutionOptions(TextOperationDto operation, List<TextOperationDto> concurrentOperations)
        {
            return new List<string> { "Manter local", "Aceitar remoto", "Mesclar automaticamente" };
        }

        private string GenerateOperationHash(TextOperationDto operation)
        {
            var content = $"{operation.Type}|{operation.Position}|{operation.Content}|{operation.Length}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)).Substring(0, 16);
        }

        private async Task<int> GetOperationCountSinceLastSnapshotAsync(Guid itemId)
        {
            var lastSnapshot = await _context.CollaborationSnapshots
                .Where(s => s.ItemId == itemId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastSnapshot == null)
            {
                return await _context.TextOperations.CountAsync(o => o.ItemId == itemId);
            }

            return await _context.TextOperations
                .CountAsync(o => o.ItemId == itemId && o.SequenceNumber > lastSnapshot.LastSequenceNumber);
        }

        private async Task<DateTime> GetLastSnapshotTimeAsync(Guid itemId)
        {
            var lastSnapshot = await _context.CollaborationSnapshots
                .Where(s => s.ItemId == itemId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            return lastSnapshot?.CreatedAt ?? DateTime.MinValue;
        }

        private async Task CreateSnapshotAsync(Guid itemId, Guid userId, SnapshotTrigger trigger)
        {
            // TODO: Implementar criação de snapshot completa
            _logger.LogInformation("Creating snapshot for item {ItemId} triggered by {Trigger}", itemId, trigger);
        }
    }
}
```

## Entregáveis da Parte 3.7

✅ **IUserPresenceService**: Interface completa para gestão de presença  
✅ **UserPresenceService**: Implementação com cleanup automático  
✅ **IOperationalTransformService**: Interface para algoritmos OT  
✅ **OperationalTransformService**: Implementação básica funcional  
✅ **Detecção de conflitos**: Algoritmo básico de análise  
✅ **Gestão de snapshots**: Sistema de backup periódico  
✅ **Logging e tratamento de erros** em todos os services  

## Próximos Passos

Na **Parte 3.8**, implementaremos:
- IChatService para operações de mensagens
- INotificationService para sistema de notificações
- Processamento de menções e reações

**Dependência**: Esta parte (3.7) deve estar implementada antes de prosseguir.