# Fase 3 (Parte 2) – Modelo de Domínio da Colaboração

> Esta parte contém as entidades persistentes e enums necessários para recursos de colaboração em tempo real.
> Origem: extraído e organizado do documento original grande.

## 1. Visão Geral
O domínio de colaboração abrange chat, presença, edição simultânea com OT, versionamento híbrido, métricas e auditoria. Cada entidade foi projetada para:
- Suportar escalabilidade horizontal (indices e separação de responsabilidades)
- Permitir extensões futuras (ex: CRDT, branching, análise de atividades)
- Manter audit trail completo das ações relevantes

## 2. Entidades Principais

### 2.1 ChatMessage
Representa mensagens do chat de um workspace. Suporta threads (ParentMessageId) e tipos diferenciados.
```csharp
public class ChatMessage
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;
    public DateTime CreatedAt { get; set; }
    public bool IsEdited { get; set; } = false;
    public DateTime? EditedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? ParentMessageId { get; set; }
    public ChatMessage ParentMessage { get; set; }
    public List<ChatMessage> Replies { get; set; } = new();
}
```

### 2.2 UserPresence
Estado de presença de um usuário em um workspace, com item que está sendo editado.
```csharp
public class UserPresence
{
    public Guid Id { get; set; }
    public string ConnectionId { get; set; }
    public UserPresenceStatus Status { get; set; } = UserPresenceStatus.Online;
    public DateTime LastSeenAt { get; set; }
    public string CurrentItemId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}
```

### 2.3 EditorChange (Histórico simples de mudanças)
```csharp
public class EditorChange
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}
```

### 2.4 UserCursor
```csharp
public class UserCursor
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public int? SelectionStart { get; set; }
    public int? SelectionEnd { get; set; }
    public string UserColor { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime Timestamp { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}
```

### 2.5 Notification
```csharp
public class Notification
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string ActionUrl { get; set; }
    public string ActionData { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? TriggeredById { get; set; }
    public User TriggeredBy { get; set; }
}
```

### 2.6 TextOperation (Operational Transform)
```csharp
public class TextOperation
{
    public Guid Id { get; set; }
    public OperationType Type { get; set; }
    public int Position { get; set; }
    public int Length { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public int ClientId { get; set; }
    public int SequenceNumber { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}
```

### 2.7 ConflictResolution
```csharp
public class ConflictResolution
{
    public Guid Id { get; set; }
    public ConflictType Type { get; set; }
    public ResolutionStrategy Strategy { get; set; }
    public string OriginalOperation { get; set; }
    public string TransformedOperation { get; set; }
    public string ResolutionData { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime ResolvedAt { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public User ResolvedBy { get; set; }
}
```

### 2.8 CollaborationSnapshot
```csharp
public class CollaborationSnapshot
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public int OperationCount { get; set; }
    public SnapshotTrigger Trigger { get; set; }
    public DateTime CreatedAt { get; set; }
    public long ContentSize { get; set; }
    public string ContentHash { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedBy { get; set; }
}
```

### 2.9 CollaborationMetrics
```csharp
public class CollaborationMetrics
{
    public Guid Id { get; set; }
    public MetricType Type { get; set; }
    public string MetricName { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
    public string Tags { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid? UserId { get; set; }
    public User User { get; set; }
}
```

### 2.10 CollaborationAuditLog
```csharp
public class CollaborationAuditLog
{
    public Guid Id { get; set; }
    public AuditAction Action { get; set; }
    public string Resource { get; set; }
    public string ResourceId { get; set; }
    public string Details { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
}
```

## 3. Enums
```csharp
public enum ChatMessageType { Text = 0, System = 1, File = 2, Code = 3, Image = 4, Notification = 5 }
public enum UserPresenceStatus { Online = 0, Away = 1, Busy = 2, Offline = 3 }
public enum NotificationType { WorkspaceInvitation = 0, ItemCreated = 1, ItemUpdated = 2, PhasePromotion = 3, UserJoined = 4, UserLeft = 5, ChatMention = 6, System = 7 }
public enum NotificationPriority { Low = 0, Normal = 1, High = 2, Critical = 3 }
public enum OperationType { Insert = 0, Delete = 1, Retain = 2 }
public enum ConflictType { Simple = 0, Complex = 1, Critical = 2 }
public enum ResolutionStrategy { AutomaticMerge = 0, UserChoice = 1, FirstWins = 2, LastWins = 3, ManualReview = 4 }
public enum SnapshotTrigger { OperationCount = 0, TimeInterval = 1, Manual = 2, Conflict = 3, Shutdown = 4 }
public enum MetricType { Counter = 0, Gauge = 1, Histogram = 2, Timer = 3 }
public enum AuditAction { Connect = 0, Disconnect = 1, JoinWorkspace = 2, LeaveWorkspace = 3, EditItem = 4, SendMessage = 5, ViewPresence = 6, ResolveConflict = 7, CreateSnapshot = 8, AccessDenied = 9 }
```

## 4. Considerações de Modelagem
- Índices estratégicos para queries frequentes (ex: presença por workspace, operações por sequence number).
- Hash de conteúdo (`ContentHash`) otimiza detecção de duplicidade de snapshots.
- Campos JSON (Tags, ResolutionData, Details) permitem extensão sem migrations imediatas.

## 5. Próxima Leitura
Avance para a **Parte 3 – Infraestrutura EF Core e Parâmetros de Sistema**.

---
_Parte 2 concluída. Próximo: 03-03-infra-ef-parametros.md_
