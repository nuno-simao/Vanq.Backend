# Fase 3 (Parte 7) – Serviços Núcleo de Colaboração

> Serviços diretamente consumidos pelo Hub para viabilizar presença, edição colaborativa (OT), chat e notificações.

## 1. Visão Geral
Estes serviços encapsulam lógica de domínio e persistência, oferecendo API coerente ao Hub e controllers REST. Focam em consistência, integridade e extensibilidade.

## 2. Serviço de Presença – IUserPresenceService
Responsável por estado online/offline, item atual e limpeza de conexões órfãs.
```csharp
public interface IUserPresenceService
{
    Task SetUserPresenceAsync(Guid workspaceId, Guid userId, string connectionId, UserPresenceStatus status);
    Task SetUserOfflineAsync(string connectionId);
    Task UpdateCurrentItemAsync(Guid workspaceId, Guid userId, string currentItemId);
    Task<List<UserPresenceDto>> GetWorkspacePresenceAsync(Guid workspaceId);
    Task CleanupStaleConnectionsAsync();
    Task<int> GetWorkspaceActiveCountAsync(Guid workspaceId);
    Task<int> GetItemActiveEditorsCountAsync(Guid itemId);
    Task<List<UserPresenceDto>> GetItemActiveEditorsAsync(Guid itemId);
    Task<List<string>> GetUserActiveItemsAsync(Guid userId);
}
```
Pontos-chave:
- `LastSeenAt` atualizado em cada interação relevante.
- Limite de usuários simultâneos por workspace (validado no Hub, configurável via parâmetros).

## 3. Serviço de Operational Transform – IOperationalTransformService
Coordena processamento, transformação, conflito e snapshots.
```csharp
public interface IOperationalTransformService
{
    Task<OperationTransformResult> ProcessOperationAsync(Guid itemId, TextOperationDto operation, Guid userId);
    Task ApplyOperationAsync(Guid itemId, TextOperationDto operation);
    Task<ConflictResolutionResult> ResolveConflictAsync(Guid conflictId, ResolutionStrategy strategy, TextOperationDto resolvedOperation, Guid userId);
    Task<ConflictResolutionResult> ResolveConflictAsync(ConflictResolutionDto conflict, ResolutionStrategy strategy);
    Task<CollaborationSnapshotDto> CreateSnapshotAsync(Guid itemId, Guid userId, SnapshotTrigger trigger);
    Task<CollaborationSnapshotDto> CreateSnapshotIfNeededAsync(Guid itemId, Guid userId, SnapshotTrigger trigger);
    Task<CollaborationSnapshotDto> GetLatestSnapshotAsync(Guid itemId);
    Task<List<TextOperationDto>> GetOperationsSinceSnapshotAsync(Guid itemId, Guid? snapshotId);
    Task LockItemAsync(Guid itemId, Guid userId);
    Task UnlockItemAsync(Guid itemId, Guid userId);
    Task<bool> IsItemLockedAsync(Guid itemId);
}
```
Características:
- Sequenciamento incremental (`SequenceNumber`) para reconstrução determinística.
- Estratégia de snapshot híbrida (por contagem de operações, tempo ou triggers).
- Transformação simplificada inspirada em Google Wave OT.
- Limpeza de snapshots antigos via política (retention + max per item).

## 4. Serviço de Chat – IChatService
Mensagens com suporte a replies e histórico paginado.
```csharp
public interface IChatService
{
    Task<ChatMessageDto> SendMessageAsync(Guid workspaceId, Guid userId, SendChatMessageRequest request);
    Task<ChatMessageDto> EditMessageAsync(Guid messageId, Guid userId, EditChatMessageRequest request);
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
    Task<List<ChatMessageDto>> GetWorkspaceChatHistoryAsync(Guid workspaceId, Guid userId, int page = 1, int pageSize = 50);
    Task<List<ChatMessageDto>> GetMessageRepliesAsync(Guid messageId, Guid userId);
}
```
Regras principais:
- Apenas autor pode editar e dentro de janela temporal (ex: 24h).
- Replies carregadas aninhadas no histórico.

## 5. Serviço de Notificações – INotificationService
Criação e entrega em tempo real de notificações distributivas.
```csharp
public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int page = 1, int pageSize = 20);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task<bool> MarkAllAsReadAsync(Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task SendWorkspaceNotificationAsync(Guid workspaceId, NotificationType type, string title, string message, Guid? triggeredById = null);
}
```
Considerações:
- Broadcast seletivo exceto o autor (triggeredById).
- Extensível para push externo (e-mail / mobile) em fases futuras.

## 6. Estratégias Chave
| Serviço | Estratégia | Benefício |
|---------|-----------|-----------|
| Presença | Update discreto + cleanup periódico | Estado consistente sem custo alto |
| OT | Transform incremental + snapshot híbrido | Performance e recuperação rápida |
| Chat | Paginação decrescente, reordenação ascendente | UX de histórico fluido |
| Notificações | Filtro por usuário + prioridade | Relevância e não poluição |

## 7. Erros e Exceções
- OT: Lançar exceções internas logadas; hub converte em mensagem genérica.
- Chat: Acesso negado → Unauthorized; validação tardia de replies inexistentes.
- Notificações: Falhas parciais logadas e compensadas se necessário.

## 8. Otimizações Futuras Sugeridas
| Área | Ideia |
|------|-------|
| Presença | Migrar estado quente para Redis (TTL) |
| OT | Introduzir CRDT para operações offline-first |
| Chat | Index full-text para busca de mensagens |
| Notificações | Agrupamento e digests periódicos |
| Snapshots | Compressão (gzip ou Brotli) + diff incremental |

## 9. Interação com Métricas / Auditoria
- Incrementos de contadores (ex: `edit_operations`, `chat_messages`) delegados ao Hub ou serviço conforme necessidade de precisão.
- Auditoria focada em eventos de segurança e mutações (Join, Leave, Edit, Conflict, Notification).

## 10. Próxima Leitura
Continue para **Parte 8 – Serviços Cross-Cutting (Métricas, Rate Limiting, Auditoria)**.

---
_Parte 7 concluída. Próximo: 03-08-servicos-cross-cutting.md_
