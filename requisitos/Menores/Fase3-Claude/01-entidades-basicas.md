# Fase 3.1: Entidades, DTOs e Requests Completos

## Contexto da Fase 3

Esta é a **terceira fase** de implementação do backend IDE. Com o sistema de workspace estabelecido na Fase 2, agora implementaremos as **funcionalidades colaborativas em tempo real** usando SignalR.

**Pré-requisitos**: Fases 1 e 2 devem estar 100% funcionais

## Objetivos da Fase 3

✅ **SignalR Hub** completo para comunicação em tempo real  
✅ **Edição colaborativa** de itens com sincronização  
✅ **Sistema de chat** por workspace  
✅ **Notificações em tempo real** para ações do workspace  
✅ **Gestão de cursores** múltiplos durante edição  
✅ **Indicadores de presença** de usuários ativos  

## Funcionalidades de Tempo Real

- **Edição Simultânea**: Múltiplos usuários editando o mesmo item
- **Chat em Workspace**: Comunicação entre colaboradores
- **Notificações**: Mudanças, convites, promoções de fase
- **Presença**: Usuários ativos no workspace
- **Cursores em Tempo Real**: Posição dos cursores de outros usuários

## 1. Enums para Colaboração

### 1.1 Enums de Chat

#### IDE.Domain/Enums/ChatMessageType.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum ChatMessageType
    {
        Text = 0,
        Image = 1,
        File = 2,
        Code = 3,
        System = 4,
        Mention = 5
    }
}
```

### 1.2 Enums de Presença

#### IDE.Domain/Entities/Realtime/Enums/UserPresenceStatus.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    public enum UserPresenceStatus
    {
        Online = 0,
        Away = 1,
        Busy = 2,
        Offline = 3
    }
}
```

### 1.3 Enums de Operational Transform

#### IDE.Domain/Enums/TextOperationType.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum TextOperationType
    {
        Insert = 0,
        Delete = 1,
        Retain = 2,
        Replace = 3
    }
}
```

#### IDE.Domain/Enums/ConflictType.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum ConflictType
    {
        None = 0,
        Simple = 1,
        Complex = 2,
        Structural = 3
    }
}
}
```

#### IDE.Domain/Entities/Realtime/Enums/ConflictSeverity.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    public enum ConflictSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
```

#### IDE.Domain/Entities/Realtime/Enums/ResolutionStrategy.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    public enum ResolutionStrategy
    {
        AutoMerge = 0,
        PreferLocal = 1,
        PreferRemote = 2,
        ManualReview = 3
    }
}
```

#### IDE.Domain/Entities/Realtime/Enums/SnapshotTrigger.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    public enum SnapshotTrigger
    {
        OperationCount = 0,
        TimeInterval = 1,
        Shutdown = 2,
        Manual = 3
    }
}
```

### 1.4 Enums de Notificação

#### IDE.Domain/Enums/NotificationType.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum NotificationType
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3,
        WorkspaceInvite = 4,
        PhasePromotion = 5,
        ItemUpdate = 6,
        ChatMention = 7,
        System = 8
    }
}
```

#### IDE.Domain/Enums/NotificationPriority.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum NotificationPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3
    }
}
```

### 1.5 Enums de Auditoria

#### IDE.Domain/Enums/AuditAction.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum AuditAction
    {
        Create = 0,
        Read = 1,
        Update = 2,
        Delete = 3,
        Login = 4,
        Logout = 5,
        Join = 6,
        Leave = 7,
        Invite = 8,
        Promote = 9,
        Demote = 10,
        Export = 11,
        Import = 12
    }
}
```

## 2. Entidades Básicas

### 2.1 ChatMessage - Sistema de Chat

#### IDE.Domain/Entities/Realtime/ChatMessage.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities;
using IDE.Domain.Entities.Workspace;
using IDE.Domain.Enums;

namespace IDE.Domain.Entities.Realtime
{
    public class ChatMessage
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        
        public ChatMessageType Type { get; set; } = ChatMessageType.Text;
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public bool IsEdited { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        
        // Relacionamentos
        public Guid WorkspaceId { get; set; }
        public virtual Workspace Workspace { get; set; } = null!;
        
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        
        public Guid? ParentMessageId { get; set; } // Para threads
        public virtual ChatMessage? ParentMessage { get; set; }
        
        // Anexos
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
        
        // Metadata
        public string? Metadata { get; set; } // JSON
        
        // Coleções
        public virtual ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
        public virtual ICollection<MessageReadReceipt> ReadReceipts { get; set; } = new List<MessageReadReceipt>();
        public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
    }
}
```

### 2.2 MessageReaction - Reações em Mensagens

#### IDE.Domain/Entities/Realtime/MessageReaction.cs
```csharp
using IDE.Domain.Entities.Users;

namespace IDE.Domain.Entities.Realtime
{
    public class MessageReaction
    {
        public Guid Id { get; set; }
        
        public string Emoji { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        // Relacionamentos
        public Guid MessageId { get; set; }
        public virtual ChatMessage Message { get; set; } = null!;
        
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
```

### 2.3 MessageReadReceipt - Controle de Leitura

#### IDE.Domain/Entities/Realtime/MessageReadReceipt.cs
```csharp
using IDE.Domain.Entities.Users;

namespace IDE.Domain.Entities.Realtime
{
    public class MessageReadReceipt
    {
        public Guid Id { get; set; }
        
        public DateTime ReadAt { get; set; }
        
        // Relacionamentos
        public Guid MessageId { get; set; }
        public virtual ChatMessage Message { get; set; } = null!;
        
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
```

### 2.4 UserPresence - Presença de Usuários

#### IDE.Domain/Entities/Realtime/UserPresence.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Domain.Entities.Users;
using IDE.Domain.Entities.Workspaces;

namespace IDE.Domain.Entities.Realtime
{
    public class UserPresence
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ConnectionId { get; set; } = string.Empty;
        
        public UserPresenceStatus Status { get; set; } = UserPresenceStatus.Online;
        
        public DateTime ConnectedAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime? DisconnectedAt { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        
        public string? CurrentItemId { get; set; } // Item sendo editado
        
        // Informações de conexão
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        
        // Relacionamentos
        public Guid WorkspaceId { get; set; }
        public virtual Workspace Workspace { get; set; } = null!;
        
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
```

### 2.5 TextOperation - Operações de Texto

#### IDE.Domain/Entities/Realtime/TextOperation.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Domain.Entities.Users;

namespace IDE.Domain.Entities.Realtime
{
    public class TextOperation
    {
        public Guid Id { get; set; }
        
        public Guid ItemId { get; set; } // ID do WorkspaceItem
        
        public TextOperationType Type { get; set; }
        
        public int Position { get; set; }
        
        public string Content { get; set; } = string.Empty;
        
        public int? Length { get; set; } // Para operações de delete
        
        public long SequenceNumber { get; set; }
        
        public DateTime ClientTimestamp { get; set; }
        public DateTime ServerTimestamp { get; set; }
        
        [MaxLength(32)]
        public string OperationHash { get; set; } = string.Empty;
        
        // Metadata para debugging
        public string? Metadata { get; set; } // JSON
        
        // Relacionamentos
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
```

### 2.6 CollaborationSnapshot - Snapshots de Estado

#### IDE.Domain/Entities/Realtime/CollaborationSnapshot.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Domain.Entities.Users;

namespace IDE.Domain.Entities.Realtime
{
    public class CollaborationSnapshot
    {
        public Guid Id { get; set; }
        
        public Guid ItemId { get; set; } // ID do WorkspaceItem
        
        public string Content { get; set; } = string.Empty;
        
        public string ContentHash { get; set; } = string.Empty;
        
        public long LastSequenceNumber { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public SnapshotTrigger TriggerReason { get; set; }
        
        // Relacionamentos
        public Guid CreatedByUserId { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;
    }
}
```

### 2.7 Notification - Sistema de Notificações

#### IDE.Domain/Entities/Realtime/Notification.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Domain.Entities.Workspaces;

namespace IDE.Domain.Entities.Realtime
{
    public class Notification
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;
        
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        // Ação da notificação
        public string? ActionUrl { get; set; }
        public string? ActionData { get; set; } // JSON
        
        // Relacionamentos
        public Guid? WorkspaceId { get; set; } // Null = notificação global
        public virtual Workspace? Workspace { get; set; }
        
        // Coleções
        public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    }
}
```

### 2.8 UserNotification - Notificações por Usuário

#### IDE.Domain/Entities/Realtime/UserNotification.cs
```csharp
using IDE.Domain.Entities.Users;

namespace IDE.Domain.Entities.Realtime
{
    public class UserNotification
    {
        public Guid Id { get; set; }
        
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Relacionamentos
        public Guid NotificationId { get; set; }
        public virtual Notification Notification { get; set; } = null!;
        
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
```

### 2.9 CollaborationAuditLog - Auditoria

#### IDE.Domain/Entities/Realtime/CollaborationAuditLog.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Domain.Entities.Users;

namespace IDE.Domain.Entities.Realtime
{
    public class CollaborationAuditLog
    {
        public Guid Id { get; set; }
        
        public AuditAction Action { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ResourceType { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string ResourceId { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; }
        
        public string? Details { get; set; } // JSON
        
        // Informações de contexto
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        
        // Relacionamentos
        public Guid? UserId { get; set; }
        public virtual User? User { get; set; }
    }
}
```

## 3. DTOs (Data Transfer Objects)

### 3.1 DTOs de Chat

#### IDE.Application/Realtime/DTOs/ChatMessageDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.DTOs;

namespace IDE.Application.Realtime.DTOs
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public ChatMessageType Type { get; set; }
        public Guid WorkspaceId { get; set; }
        public Guid? ParentMessageId { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
        public bool IsEdited { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserDto User { get; set; } = null!;
        public List<MessageReactionSummaryDto> Reactions { get; set; } = new();
        public List<UserDto> ReadBy { get; set; } = new();
        public int ReplyCount { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/MessageReactionDto.cs
```csharp
using IDE.Application.DTOs;

namespace IDE.Application.Realtime.DTOs
{
    public class MessageReactionDto
    {
        public Guid Id { get; set; }
        public string Emoji { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public UserDto? User { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/MessageReactionSummaryDto.cs
```csharp
using IDE.Application.DTOs;

namespace IDE.Application.Realtime.DTOs
{
    public class MessageReactionSummaryDto
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<UserDto> Users { get; set; } = new();
    }
}
```

#### IDE.Application/Realtime/DTOs/ChatStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class ChatStatsDto
    {
        public Guid WorkspaceId { get; set; }
        public int TotalMessages { get; set; }
        public int MessagesLast24Hours { get; set; }
        public int MessagesLastWeek { get; set; }
        public int MessagesLastMonth { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalThreads { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
```

### 3.2 DTOs de Presença

#### IDE.Application/Realtime/DTOs/UserPresenceDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.DTOs;

namespace IDE.Application.Realtime.DTOs
{
    public class UserPresenceDto
    {
        public string ConnectionId { get; set; } = string.Empty;
        public UserPresenceStatus Status { get; set; }
        public DateTime LastSeenAt { get; set; }
        public string? CurrentItemId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public bool IsActive { get; set; }
        public UserDto User { get; set; } = null!;
    }
}
```

#### IDE.Application/Realtime/DTOs/PresenceStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class PresenceStatsDto
    {
        public int TotalActiveUsers { get; set; }
        public int TotalActiveConnections { get; set; }
        public int UsersInLastHour { get; set; }
        public int UsersInLastDay { get; set; }
        public int ActiveWorkspaces { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
```

### 3.3 DTOs de Operational Transform

#### IDE.Application/Realtime/DTOs/TextOperationDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class TextOperationDto
    {
        public Guid? Id { get; set; }
        public TextOperationType Type { get; set; }
        public int Position { get; set; }
        public string? Content { get; set; }
        public int? Length { get; set; }
        public long SequenceNumber { get; set; }
        public DateTime ClientTimestamp { get; set; }
        public DateTime ServerTimestamp { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? OperationHash { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/OperationTransformResult.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class OperationTransformResult
    {
        public TextOperationDto? TransformedOperation { get; set; }
        public TextOperationDto OriginalOperation { get; set; } = null!;
        public long SequenceNumber { get; set; }
        public bool HasConflict { get; set; }
        public ConflictType? ConflictType { get; set; }
        public ConflictSeverity? ConflictSeverity { get; set; }
        public List<TextOperationDto>? ConflictingOperations { get; set; }
        public string? ConflictDescription { get; set; }
        public List<string>? ResolutionOptions { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/CollaborationSnapshotDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class CollaborationSnapshotDto
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public long LastSequenceNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedByUserId { get; set; }
        public SnapshotTrigger TriggerReason { get; set; }
        public int SnapshotSize { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/ConflictDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class ConflictDto
    {
        public Guid Id { get; set; }
        public ConflictType Type { get; set; }
        public ConflictSeverity Severity { get; set; }
        public TextOperationDto LocalOperation { get; set; } = null!;
        public List<TextOperationDto> ConflictingOperations { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public List<string> ResolutionOptions { get; set; } = new();
        public DateTime DetectedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/ConflictResolutionResult.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class ConflictResolutionResult
    {
        public bool Success { get; set; }
        public TextOperationDto? ResolvedOperation { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ResolvedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/DocumentIntegrityResult.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class DocumentIntegrityResult
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new();
        public string? RecommendedAction { get; set; }
        public DateTime ValidatedAt { get; set; }
    }
}
```

### 3.4 DTOs de Notificação

#### IDE.Application/Realtime/DTOs/NotificationDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; }
        public Guid? WorkspaceId { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionData { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/NotificationStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class NotificationStatsDto
    {
        public Guid? WorkspaceId { get; set; }
        public int TotalNotifications { get; set; }
        public int NotificationsLast24Hours { get; set; }
        public int NotificationsLastWeek { get; set; }
        public int UnreadNotifications { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
```

### 3.5 DTOs de Rate Limiting e Métricas

#### IDE.Application/Realtime/DTOs/RateLimitStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class RateLimitStatsDto
    {
        public Guid UserId { get; set; }
        public int EditsLastHour { get; set; }
        public int MessagesLastHour { get; set; }
        public int PresenceUpdatesLastHour { get; set; }
        public int CursorUpdatesLastHour { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/SystemMetricsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class SystemMetricsDto
    {
        public DateTime Timestamp { get; set; }
        public double TotalConnections { get; set; }
        public double TotalUsers { get; set; }
        public double TotalWorkspaces { get; set; }
        public long HubConnections { get; set; }
        public long HubDisconnections { get; set; }
        public long EditOperations { get; set; }
        public long ChatMessages { get; set; }
        public long CursorUpdates { get; set; }
        public long PresenceUpdates { get; set; }
        public long ConflictsDetected { get; set; }
        public long ConflictsResolved { get; set; }
        public double AverageEditLatency { get; set; }
        public double AverageMessageLatency { get; set; }
        public TimeSpan SystemUptime { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/WorkspaceMetricsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class WorkspaceMetricsDto
    {
        public Guid WorkspaceId { get; set; }
        public DateTime Timestamp { get; set; }
        public double ActiveUsers { get; set; }
        public double TotalConnections { get; set; }
        public long EditOperations { get; set; }
        public long ChatMessages { get; set; }
        public long CursorUpdates { get; set; }
        public long PresenceUpdates { get; set; }
        public long WorkspaceJoins { get; set; }
        public long WorkspaceLeaves { get; set; }
        public long ItemJoins { get; set; }
        public long ItemLeaves { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/UserMetricsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class UserMetricsDto
    {
        public Guid UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public long ConnectionsToday { get; set; }
        public long EditOperations { get; set; }
        public long ChatMessages { get; set; }
        public long CursorUpdates { get; set; }
        public long PresenceUpdates { get; set; }
        public long ConflictsEncountered { get; set; }
        public double AverageEditLatency { get; set; }
        public double AverageResponseTime { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/PerformanceReportDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class PerformanceReportDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime GeneratedAt { get; set; }
        public long TotalOperations { get; set; }
        public long TotalLatencyMeasurements { get; set; }
        public Dictionary<string, long> TopCounters { get; set; } = new();
        public Dictionary<string, double> AverageLatencies { get; set; } = new();
        public Dictionary<string, object> HourlyBreakdown { get; set; } = new();
    }
}
```

### 3.6 DTOs de Auditoria

#### IDE.Application/Realtime/DTOs/AuditLogDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.DTOs;

namespace IDE.Application.Realtime.DTOs
{
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public AuditAction Action { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public UserDto? User { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/AuditReportDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class AuditReportDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public Guid? WorkspaceId { get; set; }
        public Guid? UserId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalEntries { get; set; }
        public Dictionary<string, int> ActionSummary { get; set; } = new();
        public Dictionary<string, int> ResourceTypeSummary { get; set; } = new();
        public Dictionary<string, int> UserActivitySummary { get; set; } = new();
        public Dictionary<string, int> HourlyBreakdown { get; set; } = new();
        public Dictionary<string, int> TopActions { get; set; } = new();
        public List<AuditLogDto> RecentEntries { get; set; } = new();
    }
}
```

#### IDE.Application/Realtime/DTOs/AuditStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    public class AuditStatsDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalEntries { get; set; }
        public int UniqueUsers { get; set; }
        public int UniqueResources { get; set; }
        public string MostActiveAction { get; set; } = string.Empty;
        public string MostActiveResourceType { get; set; } = string.Empty;
        public int AverageEntriesPerDay { get; set; }
        public Dictionary<string, int> ActionBreakdown { get; set; } = new();
        public Dictionary<string, int> ResourceTypeBreakdown { get; set; } = new();
    }
}
```

## 4. Request Classes

### 4.1 Chat Requests

#### IDE.Application/Realtime/Requests/SendChatMessageRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.Requests
{
    public class SendChatMessageRequest
    {
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        
        public ChatMessageType Type { get; set; } = ChatMessageType.Text;
        
        public Guid? ParentMessageId { get; set; }
        
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
        
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
```

#### IDE.Application/Realtime/Requests/EditChatMessageRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Application.Realtime.Requests
{
    public class EditChatMessageRequest
    {
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
    }
}
```

### 4.2 Notification Requests

#### IDE.Application/Realtime/Requests/CreateNotificationRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.Requests
{
    public class CreateNotificationRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;
        
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        
        public Guid WorkspaceId { get; set; }
        public List<Guid> UserIds { get; set; } = new();
        
        public string? ActionUrl { get; set; }
        public string? ActionData { get; set; }
    }
}
```

## Entregáveis da Parte 3.1

✅ **9 Enums** fundamentais para colaboração  
✅ **9 Entidades** completas com relacionamentos  
✅ **18 DTOs** para todas as operações  
✅ **3 Request classes** para operações  
✅ **Relacionamentos EF** configurados  
✅ **Validações** com Data Annotations  
✅ **Namespaces** padronizados  

## Próximos Passos

Na **Parte 3.4**, implementaremos:
- SignalR Hub com conexões e grupos
- Middleware de autenticação SignalR
- Configuração de grupos por workspace

**Dependência**: Esta parte (3.1) deve estar implementada antes de prosseguir.
        
        // Relacionamentos
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Para sistema de replies
        public Guid? ParentMessageId { get; set; }
        public ChatMessage ParentMessage { get; set; }
        public List<ChatMessage> Replies { get; set; } = new();
        
        // Metadados para extensibilidade
        public string? Metadata { get; set; } // JSON com dados extras
        
        // Para anexos no futuro
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
    }
}
```

### 1.2 UserPresence - Gestão de Presença

#### IDE.Domain/Entities/Realtime/UserPresence.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    public class UserPresence
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ConnectionId { get; set; }
        
        public UserPresenceStatus Status { get; set; } = UserPresenceStatus.Online;
        
        public DateTime LastSeenAt { get; set; }
        
        // Item que está sendo editado atualmente
        [MaxLength(50)]
        public string? CurrentItemId { get; set; }
        
        // Relacionamentos
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Informações adicionais sobre a sessão
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        
        // Para clustering/load balancing
        public string? ServerInstance { get; set; }
        
        // Metadados da sessão
        public string? SessionMetadata { get; set; } // JSON
        
        // Controle de heartbeat
        public DateTime? LastHeartbeat { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
```

## 2. Enums Básicos

### 2.1 Tipos de Mensagem de Chat

#### IDE.Domain/Entities/Realtime/Enums/ChatMessageType.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Tipos de mensagens no chat colaborativo
    /// </summary>
    public enum ChatMessageType
    {
        /// <summary>
        /// Mensagem de texto normal
        /// </summary>
        Text = 0,
        
        /// <summary>
        /// Mensagem do sistema (automática)
        /// </summary>
        System = 1,
        
        /// <summary>
        /// Arquivo anexado
        /// </summary>
        File = 2,
        
        /// <summary>
        /// Código compartilhado
        /// </summary>
        Code = 3,
        
        /// <summary>
        /// Imagem compartilhada
        /// </summary>
        Image = 4,
        
        /// <summary>
        /// Notificação importante
        /// </summary>
        Notification = 5,
        
        /// <summary>
        /// Link/URL compartilhado
        /// </summary>
        Link = 6,
        
        /// <summary>
        /// Menção a outro usuário
        /// </summary>
        Mention = 7,
        
        /// <summary>
        /// Reação/emoji a outra mensagem
        /// </summary>
        Reaction = 8
    }
}
```

### 2.2 Status de Presença do Usuário

#### IDE.Domain/Entities/Realtime/Enums/UserPresenceStatus.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Status de presença dos usuários no workspace
    /// </summary>
    public enum UserPresenceStatus
    {
        /// <summary>
        /// Usuário online e ativo
        /// </summary>
        Online = 0,
        
        /// <summary>
        /// Usuário ausente (idle)
        /// </summary>
        Away = 1,
        
        /// <summary>
        /// Usuário ocupado (não deve ser interrompido)
        /// </summary>
        Busy = 2,
        
        /// <summary>
        /// Usuário offline
        /// </summary>
        Offline = 3,
        
        /// <summary>
        /// Não perturbe
        /// </summary>
        DoNotDisturb = 4,
        
        /// <summary>
        /// Em reunião
        /// </summary>
        InMeeting = 5,
        
        /// <summary>
        /// Focado no trabalho
        /// </summary>
        Focused = 6
    }
}
```

## 3. Configuração Inicial do DbContext

### 3.1 Extensão do ApplicationDbContext

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Extensão)
```csharp
// Adicionar estas propriedades à classe ApplicationDbContext existente

namespace IDE.Infrastructure.Data
{
    public partial class ApplicationDbContext : DbContext
    {
        // ... propriedades existentes das Fases 1 e 2 ...

        // Novas DbSets para colaboração em tempo real
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<UserPresence> UserPresences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ... configurações existentes das Fases 1 e 2 ...

            // Configurações de ChatMessage
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasMaxLength(2000);
                
                entity.Property(e => e.Type)
                    .HasConversion<int>();
                
                entity.Property(e => e.AttachmentUrl)
                    .HasMaxLength(500);
                
                entity.Property(e => e.AttachmentType)
                    .HasMaxLength(100);
                
                // Índices para performance
                entity.HasIndex(e => new { e.WorkspaceId, e.CreatedAt })
                    .HasDatabaseName("IX_ChatMessage_Workspace_CreatedAt");
                
                entity.HasIndex(e => e.ParentMessageId)
                    .HasDatabaseName("IX_ChatMessage_ParentMessage");
                
                entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                    .HasDatabaseName("IX_ChatMessage_User_CreatedAt");
                
                // Relacionamentos
                entity.HasOne(e => e.Workspace)
                    .WithMany()
                    .HasForeignKey(e => e.WorkspaceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.ParentMessage)
                    .WithMany(m => m.Replies)
                    .HasForeignKey(e => e.ParentMessageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurações de UserPresence
            modelBuilder.Entity<UserPresence>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.ConnectionId)
                    .IsRequired()
                    .HasMaxLength(100);
                
                entity.Property(e => e.Status)
                    .HasConversion<int>();
                
                entity.Property(e => e.CurrentItemId)
                    .HasMaxLength(50);
                
                entity.Property(e => e.UserAgent)
                    .HasMaxLength(500);
                
                entity.Property(e => e.IpAddress)
                    .HasMaxLength(45); // IPv6 support
                
                entity.Property(e => e.ServerInstance)
                    .HasMaxLength(100);
                
                // Índices únicos e de performance
                entity.HasIndex(e => e.ConnectionId)
                    .IsUnique()
                    .HasDatabaseName("IX_UserPresence_ConnectionId_Unique");
                
                entity.HasIndex(e => new { e.WorkspaceId, e.UserId })
                    .HasDatabaseName("IX_UserPresence_Workspace_User");
                
                entity.HasIndex(e => new { e.Status, e.LastSeenAt })
                    .HasDatabaseName("IX_UserPresence_Status_LastSeen");
                
                entity.HasIndex(e => e.CurrentItemId)
                    .HasDatabaseName("IX_UserPresence_CurrentItem");
                
                // Relacionamentos
                entity.HasOne(e => e.Workspace)
                    .WithMany()
                    .HasForeignKey(e => e.WorkspaceId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
```

## 4. DTOs Básicos para Comunicação

### 4.1 DTO para ChatMessage

#### IDE.Application/Realtime/DTOs/ChatMessageDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.DTOs.Users;

namespace IDE.Application.Realtime.DTOs
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; }
        public ChatMessageType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        
        // Informações do usuário
        public UserDto User { get; set; }
        
        // Para replies
        public Guid? ParentMessageId { get; set; }
        public List<ChatMessageDto> Replies { get; set; } = new();
        
        // Anexos
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
        
        // Metadados
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
```

### 4.2 DTO para UserPresence

#### IDE.Application/Realtime/DTOs/UserPresenceDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.DTOs.Users;

namespace IDE.Application.Realtime.DTOs
{
    public class UserPresenceDto
    {
        public Guid Id { get; set; }
        public string ConnectionId { get; set; }
        public UserPresenceStatus Status { get; set; }
        public DateTime LastSeenAt { get; set; }
        public string? CurrentItemId { get; set; }
        
        // Informações do usuário
        public UserDto User { get; set; }
        
        // Informações da sessão
        public string? UserAgent { get; set; }
        public string? ServerInstance { get; set; }
        
        // Status detalhado
        public DateTime? LastHeartbeat { get; set; }
        public bool IsActive { get; set; }
        
        // Para UI
        public string StatusText => GetStatusText();
        public string StatusColor => GetStatusColor();
        
        private string GetStatusText()
        {
            return Status switch
            {
                UserPresenceStatus.Online => "Online",
                UserPresenceStatus.Away => "Ausente",
                UserPresenceStatus.Busy => "Ocupado",
                UserPresenceStatus.Offline => "Offline",
                UserPresenceStatus.DoNotDisturb => "Não perturbe",
                UserPresenceStatus.InMeeting => "Em reunião",
                UserPresenceStatus.Focused => "Focado",
                _ => "Desconhecido"
            };
        }
        
        private string GetStatusColor()
        {
            return Status switch
            {
                UserPresenceStatus.Online => "#4CAF50",
                UserPresenceStatus.Away => "#FF9800",
                UserPresenceStatus.Busy => "#F44336",
                UserPresenceStatus.Offline => "#9E9E9E",
                UserPresenceStatus.DoNotDisturb => "#E91E63",
                UserPresenceStatus.InMeeting => "#9C27B0",
                UserPresenceStatus.Focused => "#2196F3",
                _ => "#757575"
            };
        }
    }
}
```

## 5. Requests Básicos

### 5.1 Request para Enviar Mensagem

#### IDE.Application/Realtime/Requests/SendChatMessageRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.Requests
{
    public class SendChatMessageRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; }
        
        public ChatMessageType Type { get; set; } = ChatMessageType.Text;
        
        public Guid? ParentMessageId { get; set; }
        
        // Para anexos
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public long? AttachmentSize { get; set; }
        
        // Metadados opcionais
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
```

### 5.2 Request para Edição de Mensagem

#### IDE.Application/Realtime/Requests/EditChatMessageRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Application.Realtime.Requests
{
    /// <summary>
    /// Request para editar mensagem de chat existente
    /// </summary>
    public class EditChatMessageRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; }
    }
}
```

### 5.3 Request para Criação de Notificação

#### IDE.Application/Realtime/Requests/CreateNotificationRequest.cs
```csharp
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.Requests
{
    /// <summary>
    /// Request para criar notificação do sistema
    /// </summary>
    public class CreateNotificationRequest
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Message { get; set; }
        
        public NotificationType Type { get; set; } = NotificationType.Info;
        
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        
        [Required]
        public Guid WorkspaceId { get; set; }
        
        /// <summary>
        /// Lista de usuários que devem receber a notificação
        /// Se vazia, envia para todos os usuários do workspace
        /// </summary>
        public List<Guid> UserIds { get; set; } = new();
        
        /// <summary>
        /// URL de ação para quando a notificação for clicada
        /// </summary>
        public string? ActionUrl { get; set; }
        
        /// <summary>
        /// Dados adicionais em JSON para a ação
        /// </summary>
        public string? ActionData { get; set; }
    }
}
```

### 5.4 Request para Atualizar Presença

#### IDE.Application/Realtime/Requests/UpdatePresenceRequest.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.Requests
{
    public class UpdatePresenceRequest
    {
        public UserPresenceStatus Status { get; set; }
        public string? CurrentItemId { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
```

## 6. DTOs de Estatísticas

### 6.1 DTOs de Chat

#### IDE.Application/Realtime/DTOs/ChatStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Estatísticas do sistema de chat
    /// </summary>
    public class ChatStatsDto
    {
        public int TotalMessages { get; set; }
        public int UnreadMessages { get; set; }
        public int ActiveUsers { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessagesToday { get; set; }
        public int ReactionsCount { get; set; }
        public double AverageMessagesPerHour { get; set; }
        public Dictionary<string, int> MessageTypeDistribution { get; set; } = new();
    }
}
```

### 6.2 DTOs de Notificações

#### IDE.Application/Realtime/DTOs/NotificationStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Estatísticas do sistema de notificações
    /// </summary>
    public class NotificationStatsDto
    {
        public int TotalNotifications { get; set; }
        public int UnreadNotifications { get; set; }
        public int NotificationsToday { get; set; }
        public int CriticalNotifications { get; set; }
        public double ReadRate { get; set; }
        public TimeSpan AverageReadTime { get; set; }
        public Dictionary<string, int> NotificationTypeDistribution { get; set; } = new();
        public Dictionary<string, int> PriorityDistribution { get; set; } = new();
    }
}
```

### 6.3 DTOs de Presença

#### IDE.Application/Realtime/DTOs/PresenceStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Estatísticas de presença de usuários
    /// </summary>
    public class PresenceStatsDto
    {
        public int TotalUsers { get; set; }
        public int OnlineUsers { get; set; }
        public int AwayUsers { get; set; }
        public int BusyUsers { get; set; }
        public int OfflineUsers { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public int PeakConcurrentUsers { get; set; }
        public DateTime? PeakTime { get; set; }
        public Dictionary<string, int> ActivityByHour { get; set; } = new();
    }
}
```

### 6.4 DTOs de Métricas do Sistema

#### IDE.Application/Realtime/DTOs/SystemMetricsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Métricas gerais do sistema
    /// </summary>
    public class SystemMetricsDto
    {
        public int TotalWorkspaces { get; set; }
        public int ActiveWorkspaces { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalMessages { get; set; }
        public int TotalNotifications { get; set; }
        public long TotalOperations { get; set; }
        public double SystemUptime { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorRate { get; set; }
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
        public DateTime CollectedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/DTOs/WorkspaceMetricsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Métricas específicas de um workspace
    /// </summary>
    public class WorkspaceMetricsDto
    {
        public Guid WorkspaceId { get; set; }
        public string WorkspaceName { get; set; } = string.Empty;
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalMessages { get; set; }
        public int TotalNotifications { get; set; }
        public int TotalOperations { get; set; }
        public DateTime? LastActivity { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public double AverageUsersPerHour { get; set; }
        public Dictionary<string, int> UserActivityDistribution { get; set; } = new();
        public Dictionary<string, int> OperationTypeDistribution { get; set; } = new();
    }
}
```

#### IDE.Application/Realtime/DTOs/UserMetricsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Métricas específicas de um usuário
    /// </summary>
    public class UserMetricsDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public int MessagesSent { get; set; }
        public int NotificationsReceived { get; set; }
        public int OperationsPerformed { get; set; }
        public DateTime? LastSeen { get; set; }
        public int WorkspacesAccessed { get; set; }
        public Dictionary<string, int> ActivityByWorkspace { get; set; } = new();
        public Dictionary<string, TimeSpan> TimeByWorkspace { get; set; } = new();
    }
}
```

### 6.5 DTOs de Relatórios

#### IDE.Application/Realtime/DTOs/PerformanceReportDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Relatório de performance do sistema
    /// </summary>
    public class PerformanceReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public double AverageResponseTime { get; set; }
        public double PeakResponseTime { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double SuccessRate { get; set; }
        public int ConcurrentUsersMax { get; set; }
        public double AverageConcurrentUsers { get; set; }
        public Dictionary<string, double> EndpointPerformance { get; set; } = new();
        public Dictionary<string, int> ErrorDistribution { get; set; } = new();
        public List<PerformanceDataPoint> TimeSeriesData { get; set; } = new();
    }
    
    public class PerformanceDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double ResponseTime { get; set; }
        public int RequestCount { get; set; }
        public int ConcurrentUsers { get; set; }
    }
}
```

### 6.6 DTOs de Auditoria

#### IDE.Application/Realtime/DTOs/AuditReportDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Relatório de auditoria
    /// </summary>
    public class AuditReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalEvents { get; set; }
        public int UniqueUsers { get; set; }
        public int UniqueWorkspaces { get; set; }
        public Dictionary<AuditAction, int> ActionDistribution { get; set; } = new();
        public Dictionary<string, int> UserActivityRanking { get; set; } = new();
        public Dictionary<string, int> WorkspaceActivityRanking { get; set; } = new();
        public List<AuditEventSummary> TopEvents { get; set; } = new();
        public List<SecurityIncident> SecurityIncidents { get; set; } = new();
    }
    
    public class AuditEventSummary
    {
        public AuditAction Action { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string WorkspaceName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }
    
    public class SecurityIncident
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = string.Empty;
    }
}
```

#### IDE.Application/Realtime/DTOs/AuditStatsDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Estatísticas de auditoria
    /// </summary>
    public class AuditStatsDto
    {
        public int TotalEvents { get; set; }
        public int EventsToday { get; set; }
        public int UniqueUsers { get; set; }
        public int UniqueWorkspaces { get; set; }
        public int SecurityIncidents { get; set; }
        public Dictionary<AuditAction, int> ActionDistribution { get; set; } = new();
        public Dictionary<string, int> ResourceTypeDistribution { get; set; } = new();
        public Dictionary<string, int> HourlyDistribution { get; set; } = new();
        public List<string> TopActiveUsers { get; set; } = new();
        public List<string> TopActiveWorkspaces { get; set; } = new();
    }
}
```

### 6.7 DTOs de Rate Limiting

#### IDE.Application/Realtime/DTOs/RateLimitStatsDto.cs
```csharp
namespace IDE.Application.Realtime.DTOs
{
    /// <summary>
    /// Estatísticas de rate limiting
    /// </summary>
    public class RateLimitStatsDto
    {
        public Guid? UserId { get; set; }
        public string Identifier { get; set; } = string.Empty;
        public int RequestsInLastMinute { get; set; }
        public int RequestsInLastHour { get; set; }
        public int RequestsInLastDay { get; set; }
        public int RemainingRequestsMinute { get; set; }
        public int RemainingRequestsHour { get; set; }
        public int RemainingRequestsDay { get; set; }
        public bool IsThrottled { get; set; }
        public DateTime? ThrottledUntil { get; set; }
        public string? ThrottleReason { get; set; }
        public TimeSpan ResetIn { get; set; }
        public Dictionary<string, int> EndpointUsage { get; set; } = new();
    }
}
```

## Entregáveis da Parte 3.1

✅ **ChatMessage**: Entidade completa para sistema de chat  
✅ **UserPresence**: Gestão de presença em tempo real  
✅ **Enums básicos**: ChatMessageType e UserPresenceStatus  
✅ **Configuração DbContext**: Mapeamento das novas entidades  
✅ **DTOs**: Estruturas para comunicação  
✅ **Requests**: Contratos para operações (incluindo EditMessage e CreateNotification)  
✅ **DTOs de Estatísticas**: ChatStats, NotificationStats, PresenceStats, SystemMetrics, UserMetrics, PerformanceReport, AuditReport, RateLimitStats

## Próximos Passos

Na **Parte 3.2**, implementaremos:
- Entidades de EditorChange e UserCursor
- Notificações em tempo real
- Enums para notificações
- Configurações DbContext adicionais

**Dependência**: Esta parte (3.1) deve estar implementada e testada antes de prosseguir.