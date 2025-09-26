# Fase 3 (Parte 5) – Hub SignalR (Operações Principais)

> Esta parte cobre a primeira metade do `WorkspaceHub`, focada em conexão, presença básica, edição colaborativa, chat e notificações essenciais.

## 1. Objetivo do Hub
Fornecer um canal bidirecional autenticado para eventos de colaboração em tempo real em workspaces e itens (documentos, módulos, etc.) garantindo:
- Controle de acesso (permissões por workspace / item)
- Rate limiting e limites de concorrência
- Broadcast eficiente com possibilidade futura de sharding

## 2. Estrutura Geral (Classe)
```csharp
[Authorize]
public class WorkspaceHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly IUserPresenceService _presenceService;
    private readonly IChatService _chatService;
    private readonly INotificationService _notificationService;
    private readonly IOperationalTransformService _otService;
    private readonly ICollaborationMetricsService _metricsService;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ICollaborationAuditService _auditService;
    private readonly ILogger<WorkspaceHub> _logger;
    private readonly IHubContext<WorkspaceHub> _hubContext;

    public WorkspaceHub(/* ... injeções ... */) { /* atribuições */ }
    // ... métodos (ver seções) ...
}
```

## 3. Workspace Lifecycle
### 3.1 JoinWorkspace
Responsável por: validar acesso, aplicar rate limit, verificar capacidade e registrar presença.
```csharp
public async Task JoinWorkspace(string workspaceId)
{
    var userId = GetUserId();
    var workspaceGuid = Guid.Parse(workspaceId);

    if (!await _rateLimitingService.CheckLimitAsync(userId, "workspace_join")) { /* erro */ }
    if (!await HasWorkspaceAccess(workspaceGuid, userId)) { /* access denied */ }

    var activeUsers = await _presenceService.GetWorkspaceActiveCountAsync(workspaceGuid);
    var maxUsers = await GetSystemParameterAsync(CollaborationParameters.COLLABORATION_MAX_USERS_PER_WORKSPACE);
    if (activeUsers >= int.Parse(maxUsers)) { /* erro limite */ }

    var shardGroup = GetShardGroup(workspaceId);
    await Groups.AddToGroupAsync(Context.ConnectionId, shardGroup);
    await _presenceService.SetUserPresenceAsync(workspaceGuid, userId, Context.ConnectionId, UserPresenceStatus.Online);
    await _auditService.LogAsync(AuditAction.JoinWorkspace, "workspace", workspaceId, userId);
    await _metricsService.IncrementAsync("workspace_joins", 1, new { workspaceId });

    var user = await GetCurrentUser(userId);
    await Clients.OthersInGroup(shardGroup).SendAsync("UserJoined", new UserPresenceDto { /* ... */ });
}
```
### 3.2 LeaveWorkspace
Atualiza presença, remove do grupo e emite evento broadcast.

## 4. Item Collaboration Lifecycle
### 4.1 JoinItem
Valida acesso ao item, limite de editores simultâneos, adiciona ao grupo e retorna estado atual (snapshot + operações pendentes + editores ativos).
```csharp
public async Task JoinItem(string itemId)
{
    var item = await _context.ModuleItems.Include(i => i.Workspace).FirstOrDefaultAsync(...);
    // Validar acesso, limites e adicionar a grupo item_<id>
    var currentSnapshot = await _otService.GetLatestSnapshotAsync(itemGuid);
    var currentOperations = await _otService.GetOperationsSinceSnapshotAsync(itemGuid, currentSnapshot?.Id);
    await Clients.Caller.SendAsync("ItemSyncState", new { Snapshot = currentSnapshot, Operations = currentOperations });
}
```
### 4.2 LeaveItem
Cria snapshot se necessário (trigger Shutdown) e remove usuário do grupo.

## 5. Edição Colaborativa (Envio de Operações)
### 5.1 SendEdit
Fluxo essencial:
1. Rate limit por plano
2. Validar permissão (Editor)
3. Processar operação (OT – transform, conflito simples ou complexo)
4. Persistir e broadcast para outros
5. Criar snapshot se aplicável
```csharp
public async Task SendEdit(string itemId, TextOperationDto operation)
{
    // Verificações de plano e permissões
    var transformResult = await _otService.ProcessOperationAsync(itemGuid, operation, userId);
    if (transformResult.HasConflict) { await HandleConflict(...); return; }
    await _otService.ApplyOperationAsync(itemGuid, transformResult.TransformedOperation);
    await _metricsService.IncrementAsync("edit_operations", 1, new { itemId });
    await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId, /* details */);
    await Clients.OthersInGroup($"item_{itemId}").SendAsync("ItemEdit", new { /* operação transformada */ });
    await _otService.CreateSnapshotIfNeededAsync(itemGuid, userId, SnapshotTrigger.OperationCount);
}
```

## 6. Cursores e Seleção
### 6.1 SendCursor
Atualiza ou cria registro de cursor e envia broadcast com dados estilizados (UserColor consistente).
### 6.2 SendTextSelection
Sem persistência obrigatória — apenas broadcast + métrica.

## 7. Chat em Tempo Real
### 7.1 SendMessage
Valida plano, acessos e envia mensagem via serviço de chat. Em seguida broadcast no grupo de shard do workspace.
### 7.2 ReactToMessage
Adiciona reação ao messageId e gera atualização incremental.
### 7.3 TypingIndicator
Emite estado leve (IsTyping) – sujeito a rate limiting menos restritivo.

## 8. Notificações em Tempo Real
### 8.1 SendNotification
Restrito a nível mínimo (Editor). Broadcast de NotificationDto para o workspace (grupo shard).

## 9. Conexão e Desconexão
### 9.1 OnConnectedAsync
Incrementa métricas de conexões.
### 9.2 OnDisconnectedAsync
- Coleta itens ativos e cria snapshots se necessário.
- Marca presença offline.
- Notifica grupos relevantes.
- Audita desconexão e incrementa métricas.

## 10. Considerações de Sharding
`GetShardGroup(workspaceId)` usa hash do ID para distribuir usuários entre shards (futuro scale-out com Redis Backplane ou Azure SignalR). Parametrizável via `SIGNALR_SHARD_COUNT`.

## 11. Tratamento de Erros e Resiliência
- Erros retornados com `Clients.Caller.SendAsync("Error", mensagem)`.
- Log estruturado com `ILogger` e inserção de IDs de usuário/workspace.
- Fall-back silencioso em limites de cursor (não interromper UX).

## 12. Segurança
| Camada | Mecanismo |
|--------|----------|
| Hub | `[Authorize]` + validação explícita de acesso por workspace/item |
| Rate Limiting | Previne spam (edições e chat) |
| Auditoria | Registro de ações sensíveis (AccessDenied, Edit, Conflict) |
| Snapshot + Logs | Recuperação forense e consistência |

## 13. Dependências Diretas
- Presença: `IUserPresenceService`
- OT: `IOperationalTransformService`
- Chat: `IChatService`
- Notificações: `INotificationService`
- Métricas: `ICollaborationMetricsService`
- Auditoria: `ICollaborationAuditService`
- Limites: `IRateLimitingService`

## 14. Próxima Leitura
Aprofunde em conflitos, transformação operacional avançada e métodos auxiliares: **Parte 6 – Hub SignalR (Avançado)**.

---
_Parte 5 concluída. Próximo: 03-06-signalr-hub-avancado.md_
