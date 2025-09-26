# Fase 3.5: SignalR Hub - Edição

## Edição Colaborativa em Tempo Real

Esta parte implementa a **edição colaborativa** com **Operational Transform**, **gestão de conflitos** e **sincronização** entre múltiplos usuários editando o mesmo item simultaneamente.

**Pré-requisitos**: Parte 3.4 (SignalR Conexões) implementada

## 1. Edição Colaborativa

### 1.1 Extensão do WorkspaceHub - Edição

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs (Continuação)
```csharp
// Adicionar estes métodos à classe WorkspaceHub existente
// Imports adicionais necessários:
using IDE.Application.Services.Collaboration;
using IDE.Application.Services.Chat;
using IDE.Application.Services.Notifications;

namespace IDE.Infrastructure.Realtime
{
    public partial class WorkspaceHub : Hub
    {
        private readonly IOperationalTransformService _otService;
        private readonly IChatService _chatService;

        // NOTA: Interfaces implementadas no Grupo 3 (Services)
        // - IOperationalTransformService: Grupo 3.7 (Algoritmos OT para edição colaborativa)  
        // - IChatService: Grupo 3.7 (Operações de chat e mensagens)

        /// <summary>
        /// Usuário entra na edição de um item específico
        /// </summary>
        public async Task JoinItem(string itemId)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Rate limiting check
                if (!await _rateLimitingService.CheckLimitAsync(userId, "item_join"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para item join",
                        Code = "RATE_LIMIT_ITEM_JOIN"
                    });
                    return;
                }

                // Verificar se o item existe e o usuário tem acesso
                var item = await _context.ModuleItems
                    .Include(i => i.Workspace)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == itemGuid);

                if (item == null)
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "NotFound",
                        Message = "Item não encontrado",
                        Code = "ITEM_NOT_FOUND"
                    });
                    return;
                }

                if (!await HasWorkspaceAccess(item.WorkspaceId, userId, PermissionLevel.Editor))
                {
                    await _auditService.LogAsync(AuditAction.AccessDenied, "item", itemId, userId);
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Permissão de edição necessária",
                        Code = "ITEM_EDIT_ACCESS_DENIED"
                    });
                    return;
                }

                // Verificar limite de editores simultâneos
                var activeEditors = await _presenceService.GetItemActiveEditorsCountAsync(itemGuid);
                var maxEditors = await _systemParameterService.GetIntAsync(
                    CollaborationParameters.COLLABORATION_MAX_CONCURRENT_EDITS);
                
                if (activeEditors >= maxEditors)
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "ItemFull",
                        Message = $"Item atingiu limite de {maxEditors} editores simultâneos",
                        Code = "ITEM_EDITOR_LIMIT",
                        MaxEditors = maxEditors,
                        CurrentEditors = activeEditors
                    });
                    return;
                }

                // Entrar no grupo do item
                var itemGroup = $"item_{itemId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, itemGroup);

                // Atualizar presença para indicar que está editando este item
                await _presenceService.UpdateCurrentItemAsync(item.WorkspaceId, userId, itemId);

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId, "joined_item");

                // Enviar estado atual do item para o novo editor
                var currentSnapshot = await _otService.GetLatestSnapshotAsync(itemGuid);
                var pendingOperations = await _otService.GetOperationsSinceSnapshotAsync(
                    itemGuid, currentSnapshot?.Id);

                var activeEditorsData = await _presenceService.GetItemActiveEditorsAsync(itemGuid);
                var activeCursors = await GetItemActiveCursors(itemGuid);

                // Obter dados do usuário atual
                var currentUser = await GetCurrentUserDto(userId);

                // Resposta para o usuário que entrou
                await Clients.Caller.SendAsync("ItemJoined", new
                {
                    ItemId = itemId,
                    WorkspaceId = item.WorkspaceId.ToString(),
                    ItemName = item.Name,
                    CurrentSnapshot = currentSnapshot,
                    PendingOperations = pendingOperations,
                    ActiveEditors = activeEditorsData,
                    ActiveCursors = activeCursors,
                    JoinedAt = DateTime.UtcNow,
                    UserColor = GenerateUserColor(userId)
                });

                // Notificar outros editores do item
                await Clients.OthersInGroup(itemGroup).SendAsync("UserJoinedItem", new
                {
                    ItemId = itemId,
                    User = currentUser,
                    UserColor = GenerateUserColor(userId),
                    JoinedAt = DateTime.UtcNow
                });

                // Métricas
                await _metricsService.IncrementAsync("item_joins", 1, new { 
                    itemId, 
                    workspaceId = item.WorkspaceId.ToString() 
                });

                _logger.LogInformation("User {UserId} joined item {ItemId} for editing", userId, itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining item {ItemId} for user {UserId}", itemId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro interno ao entrar no item",
                    Code = "ITEM_JOIN_ERROR"
                });
            }
        }

        /// <summary>
        /// Usuário sai da edição de um item
        /// </summary>
        public async Task LeaveItem(string itemId)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Criar snapshot antes de sair (se configurado)
                await _otService.CreateSnapshotIfNeededAsync(itemGuid, userId, SnapshotTrigger.Shutdown);

                // Sair do grupo do item
                var itemGroup = $"item_{itemId}";
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, itemGroup);

                // Limpar item atual da presença
                var item = await _context.ModuleItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == itemGuid);

                if (item != null)
                {
                    await _presenceService.UpdateCurrentItemAsync(item.WorkspaceId, userId, null);
                }

                // Limpar cursor do usuário
                await ClearUserCursor(itemGuid, userId);

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId, "left_item");

                // Obter dados do usuário
                var currentUser = await GetCurrentUserDto(userId);

                // Notificar outros editores
                await Clients.OthersInGroup(itemGroup).SendAsync("UserLeftItem", new
                {
                    ItemId = itemId,
                    UserId = userId.ToString(),
                    User = currentUser,
                    LeftAt = DateTime.UtcNow
                });

                // Confirmar saída
                await Clients.Caller.SendAsync("ItemLeft", new
                {
                    ItemId = itemId,
                    LeftAt = DateTime.UtcNow
                });

                // Métricas
                await _metricsService.IncrementAsync("item_leaves", 1, new { itemId });

                _logger.LogInformation("User {UserId} left item {ItemId}", userId, itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving item {ItemId} for user {UserId}", itemId, userId);
            }
        }

        /// <summary>
        /// Enviar operação de edição (Operational Transform)
        /// </summary>
        public async Task SendEdit(string itemId, TextOperationDto operation)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Rate limiting por plano do usuário
                var userPlan = await GetUserPlan(userId);
                if (!await _rateLimitingService.CheckEditLimitAsync(userId, userPlan))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para edições",
                        Code = "RATE_LIMIT_EDITS"
                    });
                    return;
                }

                // Verificar acesso ao item
                var item = await _context.ModuleItems
                    .Include(i => i.Workspace)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == itemGuid);

                if (item == null || !await HasWorkspaceAccess(item.WorkspaceId, userId, PermissionLevel.Editor))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Permissão de edição necessária",
                        Code = "EDIT_ACCESS_DENIED"
                    });
                    return;
                }

                var startTime = DateTime.UtcNow;

                // Processar operação com Operational Transform
                var transformResult = await _otService.ProcessOperationAsync(itemGuid, operation, userId);

                var processingTime = DateTime.UtcNow - startTime;

                if (transformResult.HasConflict)
                {
                    // Detectou conflito - encaminhar para resolução
                    await HandleConflict(itemId, transformResult, userId);
                    return;
                }

                // Aplicar operação transformada
                await _otService.ApplyOperationAsync(itemGuid, transformResult.TransformedOperation);

                // Métricas
                await _metricsService.IncrementAsync("edit_operations", 1, new { 
                    itemId, 
                    userId = userId.ToString(),
                    operationType = operation.Type.ToString()
                });
                await _metricsService.RecordLatency("edit_latency", processingTime, new { itemId });

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId, 
                    JsonSerializer.Serialize(new { 
                        operation = operation.Type, 
                        position = operation.Position,
                        contentLength = operation.Content?.Length ?? 0,
                        sequenceNumber = transformResult.SequenceNumber
                    }));

                // Preparar operação para broadcast
                var currentUser = await GetCurrentUserDto(userId);
                var broadcastOperation = transformResult.TransformedOperation;
                broadcastOperation.UserId = userId.ToString();
                broadcastOperation.UserName = currentUser.Username;
                broadcastOperation.UserColor = GenerateUserColor(userId);

                // Broadcast para outros editores
                var itemGroup = $"item_{itemId}";
                await Clients.OthersInGroup(itemGroup).SendAsync("ItemEdit", new
                {
                    ItemId = itemId,
                    Operation = broadcastOperation,
                    SequenceNumber = transformResult.SequenceNumber,
                    Timestamp = DateTime.UtcNow,
                    ProcessingTimeMs = processingTime.TotalMilliseconds
                });

                // Confirmar aplicação para o remetente
                await Clients.Caller.SendAsync("EditApplied", new
                {
                    ItemId = itemId,
                    OperationId = operation.Id,
                    SequenceNumber = transformResult.SequenceNumber,
                    AppliedAt = DateTime.UtcNow,
                    ProcessingTimeMs = processingTime.TotalMilliseconds
                });

                // Criar snapshot se necessário
                await _otService.CreateSnapshotIfNeededAsync(itemGuid, userId, SnapshotTrigger.OperationCount);

                _logger.LogDebug(
                    "User {UserId} sent edit to item {ItemId} with sequence {Sequence} in {ProcessingTime}ms", 
                    userId, itemId, transformResult.SequenceNumber, processingTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing edit operation for item {ItemId} by user {UserId}", 
                    itemId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao processar edição. Tente novamente.",
                    Code = "EDIT_PROCESSING_ERROR"
                });

                // Métricas de erro
                await _metricsService.IncrementAsync("edit_errors", 1, new { 
                    itemId,
                    errorType = ex.GetType().Name
                });
            }
        }

        /// <summary>
        /// Processar lote de operações de edição
        /// </summary>
        public async Task SendEditBatch(string itemId, List<TextOperationDto> operations)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Verificar limite de operações por lote
                const int maxBatchSize = 50;
                if (operations.Count > maxBatchSize)
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "BatchTooLarge",
                        Message = $"Lote excede limite de {maxBatchSize} operações",
                        Code = "BATCH_SIZE_EXCEEDED",
                        MaxSize = maxBatchSize,
                        ActualSize = operations.Count
                    });
                    return;
                }

                // Rate limiting mais rigoroso para lotes
                var userPlan = await GetUserPlan(userId);
                if (!await _rateLimitingService.CheckEditLimitAsync(userId, userPlan, operations.Count))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para lote de edições",
                        Code = "RATE_LIMIT_BATCH_EDITS"
                    });
                    return;
                }

                var results = new List<OperationResult>();
                var startTime = DateTime.UtcNow;

                // Processar cada operação no lote
                foreach (var operation in operations.OrderBy(o => o.SequenceNumber))
                {
                    try
                    {
                        var transformResult = await _otService.ProcessOperationAsync(itemGuid, operation, userId);
                        
                        if (transformResult.HasConflict)
                        {
                            results.Add(new OperationResult
                            {
                                OperationId = operation.Id,
                                Success = false,
                                Error = "Conflito detectado",
                                ConflictData = transformResult
                            });
                        }
                        else
                        {
                            await _otService.ApplyOperationAsync(itemGuid, transformResult.TransformedOperation);
                            
                            results.Add(new OperationResult
                            {
                                OperationId = operation.Id,
                                Success = true,
                                SequenceNumber = transformResult.SequenceNumber,
                                TransformedOperation = transformResult.TransformedOperation
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new OperationResult
                        {
                            OperationId = operation.Id,
                            Success = false,
                            Error = ex.Message
                        });
                    }
                }

                var processingTime = DateTime.UtcNow - startTime;

                // Resposta do lote
                await Clients.Caller.SendAsync("BatchEditResult", new
                {
                    ItemId = itemId,
                    Results = results,
                    ProcessingTimeMs = processingTime.TotalMilliseconds,
                    SuccessCount = results.Count(r => r.Success),
                    ErrorCount = results.Count(r => !r.Success)
                });

                // Broadcast operações bem-sucedidas
                var successfulOps = results.Where(r => r.Success).ToList();
                if (successfulOps.Any())
                {
                    var currentUser = await GetCurrentUserDto(userId);
                    var itemGroup = $"item_{itemId}";
                    
                    await Clients.OthersInGroup(itemGroup).SendAsync("BatchItemEdit", new
                    {
                        ItemId = itemId,
                        Operations = successfulOps.Select(r => r.TransformedOperation),
                        User = currentUser,
                        UserColor = GenerateUserColor(userId),
                        ProcessedAt = DateTime.UtcNow
                    });
                }

                // Métricas
                await _metricsService.IncrementAsync("batch_edit_operations", 1, new { 
                    itemId,
                    operationCount = operations.Count,
                    successCount = results.Count(r => r.Success)
                });

                _logger.LogInformation(
                    "User {UserId} processed batch edit for item {ItemId}: {SuccessCount}/{TotalCount} operations succeeded", 
                    userId, itemId, results.Count(r => r.Success), operations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch edit for item {ItemId} by user {UserId}", 
                    itemId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao processar lote de edições",
                    Code = "BATCH_EDIT_ERROR"
                });
            }
        }

        /// <summary>
        /// Solicitar sincronização completa de um item
        /// </summary>
        public async Task RequestSync(string itemId, long lastKnownSequence = 0, string lastKnownHash = null)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "sync_request"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para sincronização",
                        Code = "RATE_LIMIT_SYNC"
                    });
                    return;
                }

                // Verificar acesso
                var item = await _context.ModuleItems
                    .Include(i => i.Workspace)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == itemGuid);

                if (item == null || !await HasWorkspaceAccess(item.WorkspaceId, userId))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Acesso negado ao item",
                        Code = "SYNC_ACCESS_DENIED"
                    });
                    return;
                }

                // Obter estado atual
                var currentSnapshot = await _otService.GetLatestSnapshotAsync(itemGuid);
                var missingOperations = await _otService.GetOperationsSinceSequenceAsync(itemGuid, lastKnownSequence);

                var isUpToDate = lastKnownSequence >= (currentSnapshot?.LastSequenceNumber ?? 0) &&
                                lastKnownHash == currentSnapshot?.ContentHash;

                // Resposta de sincronização
                await Clients.Caller.SendAsync("SyncResponse", new
                {
                    ItemId = itemId,
                    IsUpToDate = isUpToDate,
                    CurrentSnapshot = currentSnapshot,
                    MissingOperations = missingOperations,
                    ServerSequence = currentSnapshot?.LastSequenceNumber ?? 0,
                    ServerTime = DateTime.UtcNow,
                    RequestedSequence = lastKnownSequence
                });

                // Métricas
                await _metricsService.IncrementAsync("sync_requests", 1, new { 
                    itemId,
                    isUpToDate,
                    missingOpCount = missingOperations.Count
                });

                _logger.LogDebug("User {UserId} requested sync for item {ItemId}. Up to date: {IsUpToDate}, Missing ops: {MissingOps}", 
                    userId, itemId, isUpToDate, missingOperations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sync request for item {ItemId} by user {UserId}", 
                    itemId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro na sincronização",
                    Code = "SYNC_ERROR"
                });
            }
        }

        // ===== MÉTODOS AUXILIARES PARA EDIÇÃO =====

        /// <summary>
        /// Lidar com conflitos detectados
        /// </summary>
        private async Task HandleConflict(string itemId, OperationTransformResult transformResult, Guid userId)
        {
            var conflictDto = new ConflictDto
            {
                Id = Guid.NewGuid(),
                ItemId = Guid.Parse(itemId),
                Type = transformResult.ConflictType,
                Severity = transformResult.ConflictSeverity,
                LocalOperation = transformResult.OriginalOperation,
                RemoteOperation = transformResult.ConflictingOperations?.FirstOrDefault(),
                DetectedAt = DateTime.UtcNow,
                Description = transformResult.ConflictDescription
            };

            // Log de conflito
            await _auditService.LogAsync(AuditAction.ResolveConflict, "item", itemId, userId, 
                JsonSerializer.Serialize(new { 
                    conflictType = transformResult.ConflictType,
                    severity = transformResult.ConflictSeverity
                }));

            // Métricas
            await _metricsService.IncrementAsync("conflicts_detected", 1, new { 
                itemId, 
                type = transformResult.ConflictType.ToString(),
                severity = transformResult.ConflictSeverity.ToString()
            });

            if (transformResult.ConflictType == ConflictType.Simple)
            {
                // Auto-resolver conflitos simples
                var resolvedOperation = await _otService.ResolveConflictAsync(
                    conflictDto, ResolutionStrategy.AutomaticMerge);
                
                if (resolvedOperation.Success)
                {
                    await Clients.Caller.SendAsync("ConflictResolved", new
                    {
                        ItemId = itemId,
                        ConflictId = conflictDto.Id,
                        Resolution = "automatic",
                        ResolvedOperation = resolvedOperation.ResolvedOperation,
                        ResolvedAt = DateTime.UtcNow
                    });

                    await _metricsService.IncrementAsync("conflicts_auto_resolved", 1);
                }
            }
            else
            {
                // Conflitos complexos requerem intervenção do usuário
                await Clients.Caller.SendAsync("ConflictDetected", new
                {
                    ItemId = itemId,
                    Conflict = conflictDto,
                    ResolutionOptions = transformResult.ResolutionOptions,
                    RequiresUserChoice = true
                });

                await _metricsService.IncrementAsync("conflicts_user_intervention", 1);
            }
        }

        /// <summary>
        /// Gera cor consistente para o usuário
        /// </summary>
        private string GenerateUserColor(Guid userId)
        {
            var colors = new[] { 
                "#f56a00", "#7265e6", "#ffbf00", "#00a2ae", "#1890ff", 
                "#52c41a", "#eb2f96", "#fa541c", "#722ed1", "#13c2c2" 
            };
            
            var hash = userId.GetHashCode();
            return colors[Math.Abs(hash) % colors.Length];
        }

        /// <summary>
        /// Obter cursores ativos no item
        /// </summary>
        private async Task<List<UserCursorDto>> GetItemActiveCursors(Guid itemId)
        {
            var cursors = await _context.UserCursors
                .Where(c => c.ItemId == itemId && c.IsActive)
                .Include(c => c.User)
                .AsNoTracking()
                .ToListAsync();

            return cursors.Select(c => new UserCursorDto
            {
                Position = c.Position,
                Line = c.Line,
                Column = c.Column,
                SelectionStart = c.SelectionStart,
                SelectionEnd = c.SelectionEnd,
                UserId = c.UserId.ToString(),
                UserName = c.User.Username,
                UserColor = c.UserColor,
                IsActive = c.IsActive,
                Timestamp = c.Timestamp,
                SelectionType = c.SelectionType,
                EditorMode = c.EditorMode
            }).ToList();
        }

        /// <summary>
        /// Limpar cursor do usuário ao sair do item
        /// </summary>
        private async Task ClearUserCursor(Guid itemId, Guid userId)
        {
            var cursor = await _context.UserCursors
                .FirstOrDefaultAsync(c => c.ItemId == itemId && c.UserId == userId);

            if (cursor != null)
            {
                cursor.IsActive = false;
                cursor.Timestamp = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Obter plano do usuário para rate limiting
        /// </summary>
        private async Task<UserPlan> GetUserPlan(Guid userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            return user?.Plan ?? UserPlan.Free;
        }
    }

    // ===== CLASSES AUXILIARES =====

    /// <summary>
    /// Resultado de uma operação processada
    /// </summary>
    public class OperationResult
    {
        public Guid OperationId { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public long? SequenceNumber { get; set; }
        public TextOperationDto TransformedOperation { get; set; }
        public OperationTransformResult ConflictData { get; set; }
    }
}
```

## Entregáveis da Parte 3.5

✅ **JoinItem/LeaveItem** para edição colaborativa  
✅ **SendEdit** com Operational Transform completo  
✅ **SendEditBatch** para operações em lote  
✅ **RequestSync** para sincronização sob demanda  
✅ **Gestão de conflitos** automática e manual  
✅ **Cursores colaborativos** com tracking  
✅ **Snapshots automáticos** baseados em triggers  
✅ **Rate limiting** diferenciado por plano  
✅ **Métricas detalhadas** de edição  

## Próximos Passos

Na **Parte 3.6**, implementaremos:
- SendMessage para chat em tempo real
- SendCursor para cursores colaborativos
- TypingIndicator e notificações
- Eventos do sistema

**Dependência**: Esta parte (3.5) deve estar implementada e testada antes de prosseguir.