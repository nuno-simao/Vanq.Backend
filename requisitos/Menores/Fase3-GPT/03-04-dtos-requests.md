# Fase 3 (Parte 4) – DTOs e Requests de Colaboração

> Contratos de transporte usados pelo Hub SignalR e pelos endpoints REST. Incluem representações para chat, presença, edições, notificações, OT, snapshots e estatísticas.

## 1. Princípios de Design
- Campos mínimos para reconstruir estado no cliente sem múltiplas round-trips.
- Nomeclatura consistente (UserId, WorkspaceId, ItemId) em formato GUID.
- Suporte a extensões via campos `Metadata` (Dictionary<string, object>). 
- Diferenciação clara entre DTO de operação (transitório) e entidade persistida.

## 2. DTOs Primários (SignalR / REST)

### 2.1 ChatMessageDto
```csharp
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public ChatMessageType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public UserDto User { get; set; }
    public Guid? ParentMessageId { get; set; }
    public List<ChatMessageDto> Replies { get; set; } = new();
}
```

### 2.2 UserPresenceDto
```csharp
public class UserPresenceDto
{
    public Guid Id { get; set; }
    public string ConnectionId { get; set; }
    public UserPresenceStatus Status { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string CurrentItemId { get; set; }
    public UserDto User { get; set; }
}
```

### 2.3 EditorChangeDto
```csharp
public class EditorChangeDto
{
    public string Type { get; set; }
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Content { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserColor { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 2.4 UserCursorDto (Versão básica Hub)
```csharp
public class UserCursorDto
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string SelectionStart { get; set; }
    public string SelectionEnd { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserColor { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 2.5 NotificationDto
```csharp
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ActionUrl { get; set; }
    public string ActionData { get; set; }
    public UserDto TriggeredBy { get; set; }
}
```

### 2.6 TypingIndicatorDto
```csharp
public class TypingIndicatorDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public bool IsTyping { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 2.7 TextOperationDto (OT Básico)
```csharp
public class TextOperationDto
{
    public OperationType Type { get; set; }
    public int Position { get; set; }
    public int Length { get; set; }
    public string Content { get; set; }
    public int ClientId { get; set; }
    public int SequenceNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserColor { get; set; }
}
```

### 2.8 ConflictResolutionDto (Hub)
```csharp
public class ConflictResolutionDto
{
    public Guid Id { get; set; }
    public ConflictType Type { get; set; }
    public ResolutionStrategy Strategy { get; set; }
    public string ConflictDescription { get; set; }
    public List<TextOperationDto> ConflictingOperations { get; set; }
    public List<TextOperationDto> ResolutionOptions { get; set; }
    public DateTime DetectedAt { get; set; }
    public UserDto DetectedBy { get; set; }
}
```

### 2.9 CollaborationSnapshotDto
```csharp
public class CollaborationSnapshotDto
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public int OperationCount { get; set; }
    public SnapshotTrigger Trigger { get; set; }
    public DateTime CreatedAt { get; set; }
    public long ContentSize { get; set; }
    public UserDto CreatedBy { get; set; }
}
```

### 2.10 WorkspaceCollaborationStatsDto
```csharp
public class WorkspaceCollaborationStatsDto
{
    public Guid WorkspaceId { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalEdits { get; set; }
    public int TotalMessages { get; set; }
    public int ResolvedConflicts { get; set; }
    public double AverageLatency { get; set; }
    public DateTime LastActivity { get; set; }
    public List<UserPresenceDto> ActiveCollaborators { get; set; }
}
```

### 2.11 TextSelectionDto
```csharp
public class TextSelectionDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserColor { get; set; }
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string SelectedText { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

## 3. Requests (REST / Hub)
```csharp
public class SendChatMessageRequest
{
    public string Content { get; set; }
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;
    public Guid? ParentMessageId { get; set; }
}

public class EditChatMessageRequest
{
    public string Content { get; set; }
}

public class CreateNotificationRequest
{
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string ActionUrl { get; set; }
    public string ActionData { get; set; }
    public Guid? WorkspaceId { get; set; }
    public List<Guid> UserIds { get; set; } = new();
}
```

## 4. Considerações de Evolução
- Unificar variantes de DTO repetidos (ex: segunda versão de TextOperation/Presence em outra parte) em modelos versionados se necessário.
- Adicionar `CorrelationId` para tracing distribuído em operações críticas (edição/confli to).
- Incluir compressão opcional para payloads grandes (snapshot ou lista de operações).

## 5. Boas Práticas no Frontend
| Situação | Recomendações |
|----------|---------------|
| Reconexão | Re-solicitar snapshot + operações desde último sequence local |
| Latência alta | Bufferizar operações e aplicar optimistic UI |
| Conflitos complexos | Exibir diff visual + opções (estratégia sugerida) |
| Cursor/seleção | Throttle (ex: 50–100ms) para reduzir tráfego |

## 6. Próxima Leitura
Vá para a **Parte 5 – Hub SignalR (Operações Principais)**.

---
_Parte 4 concluída. Próximo: 03-05-signalr-hub-core.md_
