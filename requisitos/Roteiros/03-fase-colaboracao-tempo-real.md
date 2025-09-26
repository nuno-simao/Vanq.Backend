# Fase 3: Colaboração em Tempo Real - Backend .NET Core 8

## Contexto da Fase

Esta é a **terceira fase** de implementação do backend IDE. Com o sistema de workspace estabelecido na Fase 2, agora implementaremos as **funcionalidades colaborativas em tempo real** usando SignalR, permitindo que múltiplos usuários trabalhem simultaneamente no mesmo workspace.

**Pré-requisitos**: Fases 1 e 2 devem estar 100% funcionais

## Objetivos da Fase

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

## 1. Entidades para Tempo Real

### 1.1 Novas Entidades

#### IDE.Domain/Entities/Realtime/
```csharp
// ChatMessage.cs
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
    public Guid? ParentMessageId { get; set; } // Para respostas
    public ChatMessage ParentMessage { get; set; }
    public List<ChatMessage> Replies { get; set; } = new();
}

// UserPresence.cs
public class UserPresence
{
    public Guid Id { get; set; }
    public string ConnectionId { get; set; }
    public UserPresenceStatus Status { get; set; } = UserPresenceStatus.Online;
    public DateTime LastSeenAt { get; set; }
    public string CurrentItemId { get; set; } // Item que está editando
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}

// EditorChange.cs - Para tracking de mudanças
public class EditorChange
{
    public Guid Id { get; set; }
    public string Type { get; set; } // "insert", "delete", "replace"
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

// UserCursor.cs - Renomeado de CursorPosition para consistência
public class UserCursor
{
    public Guid Id { get; set; }
    public int Position { get; set; } // Posição única no texto
    public int? SelectionStart { get; set; } // Início da seleção
    public int? SelectionEnd { get; set; } // Fim da seleção
    public string UserColor { get; set; } // Cor do cursor do usuário
    public bool IsActive { get; set; } = true;
    public DateTime Timestamp { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}

// Notification.cs
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
    public string ActionUrl { get; set; } // URL para navegação
    public string ActionData { get; set; } // JSON com dados extras
    public Guid? WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? TriggeredById { get; set; } // Usuário que causou a notificação
    public User TriggeredBy { get; set; }
}

// TextOperation.cs - Para Operational Transform
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

// ConflictResolution.cs - Para tracking de resoluções de conflito
public class ConflictResolution
{
    public Guid Id { get; set; }
    public ConflictType Type { get; set; }
    public ResolutionStrategy Strategy { get; set; }
    public string OriginalOperation { get; set; } // JSON da operação original
    public string TransformedOperation { get; set; } // JSON da operação transformada
    public string ResolutionData { get; set; } // JSON com dados da resolução
    public DateTime DetectedAt { get; set; }
    public DateTime ResolvedAt { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public User ResolvedBy { get; set; }
}

// CollaborationSnapshot.cs - Para versionamento híbrido
public class CollaborationSnapshot
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public int OperationCount { get; set; } // Número de operações desde último snapshot
    public SnapshotTrigger Trigger { get; set; }
    public DateTime CreatedAt { get; set; }
    public long ContentSize { get; set; }
    public string ContentHash { get; set; } // Para detecção de duplicatas
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedBy { get; set; }
}

// CollaborationMetrics.cs - Para monitoramento real-time
public class CollaborationMetrics
{
    public Guid Id { get; set; }
    public MetricType Type { get; set; }
    public string MetricName { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
    public string Tags { get; set; } // JSON com tags adicionais
    public DateTime Timestamp { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid? UserId { get; set; }
    public User User { get; set; }
}

// CollaborationAuditLog.cs - Para audit trail
public class CollaborationAuditLog
{
    public Guid Id { get; set; }
    public AuditAction Action { get; set; }
    public string Resource { get; set; } // workspace, item, chat, etc.
    public string ResourceId { get; set; }
    public string Details { get; set; } // JSON com detalhes da ação
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
}

// Enums
public enum ChatMessageType
{
    Text = 0,
    System = 1,
    File = 2,
    Code = 3,
    Image = 4,
    Notification = 5
}

public enum UserPresenceStatus
{
    Online = 0,
    Away = 1,
    Busy = 2,
    Offline = 3
}

public enum NotificationType
{
    WorkspaceInvitation = 0,
    ItemCreated = 1,
    ItemUpdated = 2,
    PhasePromotion = 3,
    UserJoined = 4,
    UserLeft = 5,
    ChatMention = 6,
    System = 7
}

public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

// Novos Enums para Operational Transform
public enum OperationType
{
    Insert = 0,
    Delete = 1,
    Retain = 2
}

public enum ConflictType
{
    Simple = 0,        // Inserções/deleções não sobrepostas
    Complex = 1,       // Operações sobrepostas
    Critical = 2       // Conflitos que requerem intervenção manual
}

public enum ResolutionStrategy
{
    AutomaticMerge = 0,
    UserChoice = 1,
    FirstWins = 2,
    LastWins = 3,
    ManualReview = 4
}

public enum SnapshotTrigger
{
    OperationCount = 0,  // A cada X operações
    TimeInterval = 1,    // A cada X minutos
    Manual = 2,          // Solicitação do usuário
    Conflict = 3,        // Após resolução de conflito
    Shutdown = 4         // Ao desconectar
}

public enum MetricType
{
    Counter = 0,         // Valores incrementais
    Gauge = 1,          // Valores instantâneos
    Histogram = 2,      // Distribuição de valores
    Timer = 3           // Medição de tempo
}

public enum AuditAction
{
    Connect = 0,
    Disconnect = 1,
    JoinWorkspace = 2,
    LeaveWorkspace = 3,
    EditItem = 4,
    SendMessage = 5,
    ViewPresence = 6,
    ResolveConflict = 7,
    CreateSnapshot = 8,
    AccessDenied = 9
}
```

### 1.2 Configuração do DbContext (Extensão)

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Adições)
```csharp
public class ApplicationDbContext : DbContext
{
    // ... propriedades existentes das Fases 1 e 2 ...

    // Novas DbSets para tempo real
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<UserPresence> UserPresences { get; set; }
    public DbSet<EditorChange> EditorChanges { get; set; }
    public DbSet<UserCursor> UserCursors { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    
    // DbSets para Operational Transform
    public DbSet<TextOperation> TextOperations { get; set; }
    public DbSet<ConflictResolution> ConflictResolutions { get; set; }
    public DbSet<CollaborationSnapshot> CollaborationSnapshots { get; set; }
    
    // DbSets para Monitoramento
    public DbSet<CollaborationMetrics> CollaborationMetrics { get; set; }
    public DbSet<CollaborationAuditLog> CollaborationAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... configurações existentes das Fases 1 e 2 ...

        // Configurações de ChatMessage
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => new { e.WorkspaceId, e.CreatedAt });
            
            entity.HasOne(e => e.Workspace).WithMany().HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.ParentMessage).WithMany(m => m.Replies).HasForeignKey(e => e.ParentMessageId);
        });

        // Configurações de UserPresence
        modelBuilder.Entity<UserPresence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConnectionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CurrentItemId).HasMaxLength(50);
            entity.HasIndex(e => e.ConnectionId).IsUnique();
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId });
            
            entity.HasOne(e => e.Workspace).WithMany().HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de EditorChange
        modelBuilder.Entity<EditorChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).HasMaxLength(10000);
            entity.HasIndex(e => new { e.ItemId, e.Timestamp });
            
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de UserCursor
        modelBuilder.Entity<UserCursor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserColor).HasMaxLength(10);
            entity.HasIndex(e => new { e.ItemId, e.UserId }).IsUnique();
            
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ActionData).HasMaxLength(2000);
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt });
            
            entity.HasOne(e => e.Workspace).WithMany().HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.TriggeredBy).WithMany().HasForeignKey(e => e.TriggeredById);
        });

        // Configurações de TextOperation (Operational Transform)
        modelBuilder.Entity<TextOperation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(10000);
            entity.HasIndex(e => new { e.ItemId, e.SequenceNumber });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de ConflictResolution
        modelBuilder.Entity<ConflictResolution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalOperation).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.TransformedOperation).HasMaxLength(5000);
            entity.Property(e => e.ResolutionData).HasMaxLength(5000);
            entity.HasIndex(e => new { e.ItemId, e.DetectedAt });
            
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.ResolvedBy).WithMany().HasForeignKey(e => e.ResolvedByUserId);
        });

        // Configurações de CollaborationSnapshot
        modelBuilder.Entity<CollaborationSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => new { e.ItemId, e.CreatedAt });
            entity.HasIndex(e => e.ContentHash);
            
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId);
            entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedByUserId);
        });

        // Configurações de CollaborationMetrics
        modelBuilder.Entity<CollaborationMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Tags).HasMaxLength(1000);
            entity.HasIndex(e => new { e.MetricName, e.Timestamp });
            entity.HasIndex(e => new { e.WorkspaceId, e.Type });
            
            entity.HasOne(e => e.Workspace).WithMany().HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de CollaborationAuditLog
        modelBuilder.Entity<CollaborationAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Resource).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ResourceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            entity.HasIndex(e => new { e.Resource, e.ResourceId });
            
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Workspace).WithMany().HasForeignKey(e => e.WorkspaceId);
        });
    }
}

### 1.3 System Parameters para Colaboração

#### IDE.Domain/Entities/SystemParameter.cs (Extensão)
```csharp
// Novos parâmetros para colaboração em tempo real
public static class CollaborationParameters
{
    // Versionamento Híbrido
    public const string COLLABORATION_SNAPSHOT_EVERY_EDITS = "collaboration.snapshot.every_edits";
    public const string COLLABORATION_SNAPSHOT_EVERY_MINUTES = "collaboration.snapshot.every_minutes";
    public const string COLLABORATION_SNAPSHOT_RETENTION_DAYS = "collaboration.snapshot.retention_days";
    public const string COLLABORATION_MAX_SNAPSHOTS_PER_ITEM = "collaboration.max_snapshots_per_item";
    
    // Rate Limiting por Plano
    public const string COLLABORATION_RATE_LIMIT_FREE_EDITS = "collaboration.rate_limit.free.edits_per_minute";
    public const string COLLABORATION_RATE_LIMIT_FREE_CHAT = "collaboration.rate_limit.free.chat_per_minute";
    public const string COLLABORATION_RATE_LIMIT_FREE_CURSOR = "collaboration.rate_limit.free.cursor_per_second";
    public const string COLLABORATION_RATE_LIMIT_FREE_CONNECTIONS = "collaboration.rate_limit.free.max_connections";
    
    public const string COLLABORATION_RATE_LIMIT_PRO_EDITS = "collaboration.rate_limit.pro.edits_per_minute";
    public const string COLLABORATION_RATE_LIMIT_PRO_CHAT = "collaboration.rate_limit.pro.chat_per_minute";
    public const string COLLABORATION_RATE_LIMIT_PRO_CURSOR = "collaboration.rate_limit.pro.cursor_per_second";
    public const string COLLABORATION_RATE_LIMIT_PRO_CONNECTIONS = "collaboration.rate_limit.pro.max_connections";
    
    public const string COLLABORATION_RATE_LIMIT_ENTERPRISE_EDITS = "collaboration.rate_limit.enterprise.edits_per_minute";
    public const string COLLABORATION_RATE_LIMIT_ENTERPRISE_CHAT = "collaboration.rate_limit.enterprise.chat_per_minute";
    public const string COLLABORATION_RATE_LIMIT_ENTERPRISE_CURSOR = "collaboration.rate_limit.enterprise.cursor_per_second";
    public const string COLLABORATION_RATE_LIMIT_ENTERPRISE_CONNECTIONS = "collaboration.rate_limit.enterprise.max_connections";
    
    // Rate Limiting por Operação
    public const string COLLABORATION_RATE_LIMIT_EDIT_OPERATION = "collaboration.rate_limit.operation.edit_per_minute";
    public const string COLLABORATION_RATE_LIMIT_CHAT_OPERATION = "collaboration.rate_limit.operation.chat_per_minute";
    public const string COLLABORATION_RATE_LIMIT_CURSOR_OPERATION = "collaboration.rate_limit.operation.cursor_per_second";
    public const string COLLABORATION_RATE_LIMIT_PRESENCE_OPERATION = "collaboration.rate_limit.operation.presence_per_minute";
    
    // Segurança
    public const string COLLABORATION_ENCRYPTION_LEVEL = "collaboration.security.encryption_level"; // Basic, Medium, High
    public const string COLLABORATION_AUDIT_RETENTION_DAYS = "collaboration.security.audit_retention_days";
    public const string COLLABORATION_SESSION_TIMEOUT_MINUTES = "collaboration.security.session_timeout_minutes";
    
    // Performance
    public const string COLLABORATION_MAX_USERS_PER_WORKSPACE = "collaboration.performance.max_users_per_workspace";
    public const string COLLABORATION_MAX_CONCURRENT_EDITS = "collaboration.performance.max_concurrent_edits";
    public const string COLLABORATION_METRICS_RETENTION_DAYS = "collaboration.performance.metrics_retention_days";
    public const string COLLABORATION_CLEANUP_INTERVAL_MINUTES = "collaboration.performance.cleanup_interval_minutes";
    
    // SignalR
    public const string COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE = "collaboration.signalr.max_message_size";
    public const string COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL = "collaboration.signalr.keepalive_interval_seconds";
    public const string COLLABORATION_SIGNALR_CLIENT_TIMEOUT = "collaboration.signalr.client_timeout_seconds";
    public const string COLLABORATION_SIGNALR_RECONNECT_ATTEMPTS = "collaboration.signalr.max_reconnect_attempts";
    
    // Valores Padrão
    public static readonly Dictionary<string, string> DefaultValues = new()
    {
        // Versionamento
        { COLLABORATION_SNAPSHOT_EVERY_EDITS, "25" },
        { COLLABORATION_SNAPSHOT_EVERY_MINUTES, "10" },
        { COLLABORATION_SNAPSHOT_RETENTION_DAYS, "5" },
        { COLLABORATION_MAX_SNAPSHOTS_PER_ITEM, "25" },
        
        // Rate Limiting Free
        { COLLABORATION_RATE_LIMIT_FREE_EDITS, "100" },
        { COLLABORATION_RATE_LIMIT_FREE_CHAT, "50" },
        { COLLABORATION_RATE_LIMIT_FREE_CURSOR, "30" },
        { COLLABORATION_RATE_LIMIT_FREE_CONNECTIONS, "2" },
        
        // Rate Limiting Pro  
        { COLLABORATION_RATE_LIMIT_PRO_EDITS, "500" },
        { COLLABORATION_RATE_LIMIT_PRO_CHAT, "200" },
        { COLLABORATION_RATE_LIMIT_PRO_CURSOR, "100" },
        { COLLABORATION_RATE_LIMIT_PRO_CONNECTIONS, "10" },
        
        // Rate Limiting Enterprise
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_EDITS, "2000" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_CHAT, "1000" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_CURSOR, "500" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_CONNECTIONS, "25" },
        
        // Rate Limiting por Operação
        { COLLABORATION_RATE_LIMIT_EDIT_OPERATION, "300" },
        { COLLABORATION_RATE_LIMIT_CHAT_OPERATION, "150" },
        { COLLABORATION_RATE_LIMIT_CURSOR_OPERATION, "60" },
        { COLLABORATION_RATE_LIMIT_PRESENCE_OPERATION, "30" },
        
        // Segurança
        { COLLABORATION_ENCRYPTION_LEVEL, "Medium" },
        { COLLABORATION_AUDIT_RETENTION_DAYS, "30" },
        { COLLABORATION_SESSION_TIMEOUT_MINUTES, "60" },
        
        // Performance
        { COLLABORATION_MAX_USERS_PER_WORKSPACE, "20" },
        { COLLABORATION_MAX_CONCURRENT_EDITS, "5" },
        { COLLABORATION_METRICS_RETENTION_DAYS, "7" },
        { COLLABORATION_CLEANUP_INTERVAL_MINUTES, "30" },
        
        // SignalR
        { COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE, "1048576" }, // 1MB
        { COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL, "15" },
        { COLLABORATION_SIGNALR_CLIENT_TIMEOUT, "30" },
        { COLLABORATION_SIGNALR_RECONNECT_ATTEMPTS, "5" }
    };
}
    }
}
```

## 2. DTOs para Tempo Real

### 2.1 DTOs de SignalR

#### IDE.Application/Realtime/DTOs/
```csharp
// ChatMessageDto.cs
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

// UserPresenceDto.cs
public class UserPresenceDto
{
    public Guid Id { get; set; }
    public string ConnectionId { get; set; }
    public UserPresenceStatus Status { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string CurrentItemId { get; set; }
    public UserDto User { get; set; }
}

// EditorChangeDto.cs
public class EditorChangeDto
{
    public string Type { get; set; } // "insert", "delete", "replace"
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

// UserCursorDto.cs
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

// NotificationDto.cs
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

// TypingIndicatorDto.cs
public class TypingIndicatorDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public bool IsTyping { get; set; }
    public DateTime Timestamp { get; set; }
}

// TextOperationDto.cs - Para Operational Transform
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

// ConflictResolutionDto.cs
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

// CollaborationSnapshotDto.cs
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

// WorkspaceCollaborationStatsDto.cs
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

// TextSelectionDto.cs - Para seleção de texto colaborativa
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

### 2.2 Requests para Chat e Notificações

#### IDE.Application/Realtime/Requests/
```csharp
// SendChatMessageRequest.cs
public class SendChatMessageRequest
{
    public string Content { get; set; }
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;
    public Guid? ParentMessageId { get; set; }
}

// EditChatMessageRequest.cs
public class EditChatMessageRequest
{
    public string Content { get; set; }
}

// CreateNotificationRequest.cs
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

## 3. SignalR Hub Implementation

### 3.1 Hub Principal

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs
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

    public WorkspaceHub(
        ApplicationDbContext context,
        IUserPresenceService presenceService,
        IChatService chatService,
        INotificationService notificationService,
        IOperationalTransformService otService,
        ICollaborationMetricsService metricsService,
        IRateLimitingService rateLimitingService,
        ICollaborationAuditService auditService,
        ILogger<WorkspaceHub> logger,
        IHubContext<WorkspaceHub> hubContext)
    {
        _context = context;
        _presenceService = presenceService;
        _chatService = chatService;
        _notificationService = notificationService;
        _otService = otService;
        _metricsService = metricsService;
        _rateLimitingService = rateLimitingService;
        _auditService = auditService;
        _logger = logger;
        _hubContext = hubContext;
    }

    // Conexão e Grupos com Rate Limiting
    public async Task JoinWorkspace(string workspaceId)
    {
        var userId = GetUserId();
        var workspaceGuid = Guid.Parse(workspaceId);

        // Rate limiting check
        if (!await _rateLimitingService.CheckLimitAsync(userId, "workspace_join"))
        {
            await Clients.Caller.SendAsync("Error", "Rate limit exceeded para workspace join");
            return;
        }

        // Verificar permissão
        if (!await HasWorkspaceAccess(workspaceGuid, userId))
        {
            await _auditService.LogAsync(AuditAction.AccessDenied, "workspace", workspaceId, userId);
            await Clients.Caller.SendAsync("Error", "Acesso negado ao workspace");
            return;
        }

        // Verificar limite de usuários por workspace
        var activeUsers = await _presenceService.GetWorkspaceActiveCountAsync(workspaceGuid);
        var maxUsers = await GetSystemParameterAsync(CollaborationParameters.COLLABORATION_MAX_USERS_PER_WORKSPACE);
        
        if (activeUsers >= int.Parse(maxUsers))
        {
            await Clients.Caller.SendAsync("Error", $"Workspace atingiu limite máximo de {maxUsers} usuários");
            return;
        }

        // Entrar no grupo (com sharding support)
        var shardGroup = GetShardGroup(workspaceId);
        await Groups.AddToGroupAsync(Context.ConnectionId, shardGroup);

        // Registrar presença
        await _presenceService.SetUserPresenceAsync(workspaceGuid, userId, Context.ConnectionId, UserPresenceStatus.Online);

        // Audit log
        await _auditService.LogAsync(AuditAction.JoinWorkspace, "workspace", workspaceId, userId);

        // Metrics
        await _metricsService.IncrementAsync("workspace_joins", 1, new { workspaceId });

        // Notificar outros usuários
        var user = await GetCurrentUser(userId);
        await Clients.OthersInGroup(shardGroup).SendAsync("UserJoined", new UserPresenceDto
        {
            ConnectionId = Context.ConnectionId,
            Status = UserPresenceStatus.Online,
            LastSeenAt = DateTime.UtcNow,
            User = user
        });

        _logger.LogInformation("User {UserId} joined workspace {WorkspaceId} in shard {Shard}", 
            userId, workspaceId, shardGroup);
    }

    public async Task LeaveWorkspace(string workspaceId)
    {
        var userId = GetUserId();
        var workspaceGuid = Guid.Parse(workspaceId);

        var shardGroup = GetShardGroup(workspaceId);
        
        // Sair do grupo
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, shardGroup);

        // Atualizar presença
        await _presenceService.SetUserPresenceAsync(workspaceGuid, userId, Context.ConnectionId, UserPresenceStatus.Offline);

        // Audit log
        await _auditService.LogAsync(AuditAction.LeaveWorkspace, "workspace", workspaceId, userId);

        // Metrics
        await _metricsService.IncrementAsync("workspace_leaves", 1, new { workspaceId });

        // Notificar outros usuários
        await Clients.OthersInGroup(shardGroup).SendAsync("UserLeft", userId.ToString());

        _logger.LogInformation("User {UserId} left workspace {WorkspaceId}", userId, workspaceId);
    }

    // Edição Colaborativa com Operational Transform
    public async Task JoinItem(string itemId)
    {
        var userId = GetUserId();
        var itemGuid = Guid.Parse(itemId);

        // Rate limiting
        if (!await _rateLimitingService.CheckLimitAsync(userId, "item_join"))
        {
            await Clients.Caller.SendAsync("Error", "Rate limit exceeded para item join");
            return;
        }

        // Verificar se o item existe e o usuário tem acesso
        var item = await _context.ModuleItems
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Id == itemGuid);

        if (item == null || !await HasWorkspaceAccess(item.WorkspaceId, userId))
        {
            await _auditService.LogAsync(AuditAction.AccessDenied, "item", itemId, userId);
            await Clients.Caller.SendAsync("Error", "Acesso negado ao item");
            return;
        }

        // Verificar limite de editores simultâneos
        var activeEditors = await _presenceService.GetItemActiveEditorsCountAsync(itemGuid);
        var maxEditors = await GetSystemParameterAsync(CollaborationParameters.COLLABORATION_MAX_CONCURRENT_EDITS);
        
        if (activeEditors >= int.Parse(maxEditors))
        {
            await Clients.Caller.SendAsync("Error", $"Item atingiu limite de {maxEditors} editores simultâneos");
            return;
        }

        // Entrar no grupo do item
        var itemGroup = $"item_{itemId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, itemGroup);

        // Atualizar presença para indicar que está editando este item
        await _presenceService.UpdateCurrentItemAsync(item.WorkspaceId, userId, itemId);

        // Audit log
        await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId);

        // Enviar estado atual para novo editor
        var currentSnapshot = await _otService.GetLatestSnapshotAsync(itemGuid);
        var currentOperations = await _otService.GetOperationsSinceSnapshotAsync(itemGuid, currentSnapshot?.Id);

        await Clients.Caller.SendAsync("ItemSyncState", new
        {
            ItemId = itemId,
            Snapshot = currentSnapshot,
            Operations = currentOperations,
            ActiveEditors = await _presenceService.GetItemActiveEditorsAsync(itemGuid)
        });

        // Notificar outros editores do item
        var user = await GetCurrentUser(userId);
        await Clients.OthersInGroup(itemGroup).SendAsync("UserJoinedItem", new
        {
            ItemId = itemId,
            User = user
        });

        _logger.LogInformation("User {UserId} joined item {ItemId}", userId, itemId);
    }

    public async Task LeaveItem(string itemId)
    {
        var userId = GetUserId();

        // Criar snapshot antes de sair (se configurado)
        var itemGuid = Guid.Parse(itemId);
        await _otService.CreateSnapshotIfNeededAsync(itemGuid, userId, SnapshotTrigger.Shutdown);

        // Sair do grupo do item
        var itemGroup = $"item_{itemId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, itemGroup);

        // Limpar item atual da presença
        var item = await _context.ModuleItems.FindAsync(itemGuid);
        if (item != null)
        {
            await _presenceService.UpdateCurrentItemAsync(item.WorkspaceId, userId, null);
        }

        // Audit log
        await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId, "left_item");

        // Notificar outros editores
        await Clients.OthersInGroup(itemGroup).SendAsync("UserLeftItem", new
        {
            ItemId = itemId,
            UserId = userId.ToString()
        });

        _logger.LogInformation("User {UserId} left item {ItemId}", userId, itemId);
    }

    public async Task SendEdit(string itemId, TextOperationDto operation)
    {
        var userId = GetUserId();
        var itemGuid = Guid.Parse(itemId);

        // Rate limiting por plano do usuário
        var userPlan = await GetUserPlanAsync(userId);
        if (!await _rateLimitingService.CheckEditLimitAsync(userId, userPlan))
        {
            await Clients.Caller.SendAsync("Error", "Rate limit exceeded para edições");
            return;
        }

        // Verificar acesso ao item
        var item = await _context.ModuleItems
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Id == itemGuid);

        if (item == null || !await HasWorkspaceAccess(item.WorkspaceId, userId, PermissionLevel.Editor))
        {
            await Clients.Caller.SendAsync("Error", "Permissão de edição necessária");
            return;
        }

        try
        {
            // Processar operação com Operational Transform
            var transformResult = await _otService.ProcessOperationAsync(itemGuid, operation, userId);

            if (transformResult.HasConflict)
            {
                // Conflict detection
                await HandleConflict(itemId, transformResult, userId);
                return;
            }

            // Aplicar operação transformada
            await _otService.ApplyOperationAsync(itemGuid, transformResult.TransformedOperation);

            // Metrics
            await _metricsService.IncrementAsync("edit_operations", 1, new { itemId, userId = userId.ToString() });
            await _metricsService.RecordLatency("edit_latency", transformResult.ProcessingTime);

            // Audit log
            await _auditService.LogAsync(AuditAction.EditItem, "item", itemId, userId, 
                JsonSerializer.Serialize(new { operation = operation.Type, position = operation.Position }));

            // Broadcast para outros editores
            var user = await GetCurrentUser(userId);
            var broadcastOperation = transformResult.TransformedOperation;
            broadcastOperation.UserId = userId.ToString();
            broadcastOperation.UserName = user.Username;
            broadcastOperation.UserColor = GetUserColor(userId);

            var itemGroup = $"item_{itemId}";
            await Clients.OthersInGroup(itemGroup).SendAsync("ItemEdit", new
            {
                ItemId = itemId,
                Operation = broadcastOperation,
                SequenceNumber = transformResult.SequenceNumber
            });

            // Criar snapshot se necessário
            await _otService.CreateSnapshotIfNeededAsync(itemGuid, userId, SnapshotTrigger.OperationCount);

            _logger.LogInformation("User {UserId} sent edit to item {ItemId} with sequence {Sequence}", 
                userId, itemId, transformResult.SequenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing edit operation for item {ItemId} by user {UserId}", itemId, userId);
            await Clients.Caller.SendAsync("Error", "Erro ao processar edição. Tente novamente.");
        }
    }
    public async Task SendCursor(string itemId, UserCursorDto position)
    {
        var userId = GetUserId();
        var itemGuid = Guid.Parse(itemId);

        // Rate limiting para cursors
        if (!await _rateLimitingService.CheckCursorLimitAsync(userId))
        {
            return; // Silently ignore cursor updates if rate limited
        }

        // Atualizar posição do cursor no banco
        var existingCursor = await _context.CursorPositions
            .FirstOrDefaultAsync(c => c.ItemId == itemGuid && c.UserId == userId);

        if (existingCursor != null)
        {
            existingCursor.Line = position.Line;
            existingCursor.Column = position.Column;
            existingCursor.SelectionStart = position.SelectionStart;
            existingCursor.SelectionEnd = position.SelectionEnd;
            existingCursor.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existingCursor = new CursorPosition
            {
                Id = Guid.NewGuid(),
                Line = position.Line,
                Column = position.Column,
                SelectionStart = position.SelectionStart,
                SelectionEnd = position.SelectionEnd,
                UpdatedAt = DateTime.UtcNow,
                ItemId = itemGuid,
                UserId = userId
            };
            _context.CursorPositions.Add(existingCursor);
        }

        await _context.SaveChangesAsync();

        // Metrics
        await _metricsService.IncrementAsync("cursor_updates", 1, new { itemId });

        // Enviar posição para outros editores
        var user = await GetCurrentUser(userId);
        position.UserId = userId.ToString();
        position.UserName = user.Username;
        position.UserColor = GetUserColor(userId);
        position.UpdatedAt = existingCursor.UpdatedAt;

        var itemGroup = $"item_{itemId}";
        await Clients.OthersInGroup(itemGroup).SendAsync("CursorUpdate", new
        {
            ItemId = itemId,
            Position = position
        });
    }

    // Seleção de texto colaborativa
    public async Task SendTextSelection(string itemId, TextSelectionDto selection)
    {
        var userId = GetUserId();

        // Rate limiting
        if (!await _rateLimitingService.CheckCursorLimitAsync(userId))
        {
            return;
        }

        // Enviar seleção para outros editores
        var user = await GetCurrentUser(userId);
        selection.UserId = userId.ToString();
        selection.UserName = user.Username;
        selection.UserColor = GetUserColor(userId);
        selection.UpdatedAt = DateTime.UtcNow;

        var itemGroup = $"item_{itemId}";
        await Clients.OthersInGroup(itemGroup).SendAsync("TextSelectionUpdate", new
        {
            ItemId = itemId,
            Selection = selection
        });

        // Metrics
        await _metricsService.IncrementAsync("text_selections", 1, new { itemId });
    }

    // Chat com funcionalidades avançadas
    public async Task SendMessage(string workspaceId, SendChatMessageRequest request)
    {
        var userId = GetUserId();
        var workspaceGuid = Guid.Parse(workspaceId);

        // Rate limiting por plano
        var userPlan = await GetUserPlanAsync(userId);
        if (!await _rateLimitingService.CheckChatLimitAsync(userId, userPlan))
        {
            await Clients.Caller.SendAsync("Error", "Rate limit exceeded para chat");
            return;
        }

        // Verificar acesso
        if (!await HasWorkspaceAccess(workspaceGuid, userId))
        {
            await Clients.Caller.SendAsync("Error", "Acesso negado ao workspace");
            return;
        }

        // Criar mensagem
        var messageDto = await _chatService.SendMessageAsync(workspaceGuid, userId, request);

        // Audit log
        await _auditService.LogAsync(AuditAction.SendMessage, "chat", workspaceId, userId);

        // Metrics
        await _metricsService.IncrementAsync("chat_messages", 1, new { workspaceId });

        // Enviar para todos no workspace
        var shardGroup = GetShardGroup(workspaceId);
        await Clients.Group(shardGroup).SendAsync("MessageReceived", messageDto);

        // Processar menções se houver
        await ProcessMessageMentions(messageDto, workspaceGuid);

        _logger.LogInformation("User {UserId} sent message to workspace {WorkspaceId}", userId, workspaceId);
    }

    public async Task ReactToMessage(string messageId, string emoji)
    {
        var userId = GetUserId();
        var messageGuid = Guid.Parse(messageId);

        // Rate limiting
        if (!await _rateLimitingService.CheckChatLimitAsync(userId, await GetUserPlanAsync(userId)))
        {
            await Clients.Caller.SendAsync("Error", "Rate limit exceeded");
            return;
        }

        // Processar reação
        var reactionResult = await _chatService.AddReactionAsync(messageGuid, userId, emoji);

        if (reactionResult != null)
        {
            // Broadcast reação
            var message = await _chatService.GetMessageAsync(messageGuid);
            var shardGroup = GetShardGroup(message.WorkspaceId.ToString());
            
            await Clients.Group(shardGroup).SendAsync("MessageReactionAdded", new
            {
                MessageId = messageId,
                Emoji = emoji,
                UserId = userId.ToString(),
                TotalReactions = reactionResult.TotalCount
            });

            // Metrics
            await _metricsService.IncrementAsync("message_reactions", 1);
        }
    }

    public async Task TypingIndicator(string workspaceId, bool isTyping)
    {
        var userId = GetUserId();
        
        // Rate limiting leve para typing
        if (!await _rateLimitingService.CheckPresenceLimitAsync(userId))
        {
            return;
        }

        var user = await GetCurrentUser(userId);

        var indicator = new TypingIndicatorDto
        {
            UserId = userId.ToString(),
            UserName = user.Username,
            IsTyping = isTyping,
            Timestamp = DateTime.UtcNow
        };

        var shardGroup = GetShardGroup(workspaceId);
        await Clients.OthersInGroup(shardGroup).SendAsync("TypingIndicator", indicator);
    }

    // Conflict Resolution
    private async Task HandleConflict(string itemId, OperationTransformResult transformResult, Guid userId)
    {
        var conflictDto = new ConflictResolutionDto
        {
            Id = Guid.NewGuid(),
            Type = transformResult.ConflictType,
            Strategy = ResolutionStrategy.UserChoice, // Default para híbrido
            ConflictDescription = transformResult.ConflictDescription,
            ConflictingOperations = transformResult.ConflictingOperations,
            ResolutionOptions = transformResult.ResolutionOptions,
            DetectedAt = DateTime.UtcNow,
            DetectedBy = await GetCurrentUser(userId)
        };

        // Log conflict
        await _auditService.LogAsync(AuditAction.ResolveConflict, "item", itemId, userId, 
            JsonSerializer.Serialize(new { conflictType = transformResult.ConflictType }));

        // Metrics
        await _metricsService.IncrementAsync("conflicts_detected", 1, new { itemId, type = transformResult.ConflictType.ToString() });

        if (transformResult.ConflictType == ConflictType.Simple)
        {
            // Auto-resolve simple conflicts
            var resolvedOperation = await _otService.ResolveConflictAsync(conflictDto, ResolutionStrategy.AutomaticMerge);
            
            await Clients.Caller.SendAsync("ConflictResolved", new
            {
                ItemId = itemId,
                ConflictId = conflictDto.Id,
                Resolution = "automatic",
                ResolvedOperation = resolvedOperation
            });

            await _metricsService.IncrementAsync("conflicts_auto_resolved", 1);
        }
        else if (transformResult.ConflictType == ConflictType.Complex)
        {
            // Pause editing and show resolution modal
            await Clients.Caller.SendAsync("ConflictDetected", new
            {
                ItemId = itemId,
                Conflict = conflictDto,
                RequiresUserChoice = true
            });

            await _metricsService.IncrementAsync("conflicts_user_choice", 1);
        }
        else if (transformResult.ConflictType == ConflictType.Critical)
        {
            // Lock item and require manual review
            await _otService.LockItemAsync(Guid.Parse(itemId), userId);
            
            var itemGroup = $"item_{itemId}";
            await Clients.Group(itemGroup).SendAsync("ItemLocked", new
            {
                ItemId = itemId,
                Reason = "Critical conflict requires manual review",
                LockedBy = await GetCurrentUser(userId)
            });

            await _metricsService.IncrementAsync("conflicts_critical", 1);
        }
    }

    public async Task ResolveConflict(string itemId, string conflictId, ResolutionStrategy strategy, TextOperationDto resolvedOperation)
    {
        var userId = GetUserId();
        var itemGuid = Guid.Parse(itemId);
        var conflictGuid = Guid.Parse(conflictId);

        try
        {
            // Process conflict resolution
            var result = await _otService.ResolveConflictAsync(conflictGuid, strategy, resolvedOperation, userId);

            if (result.Success)
            {
                // Apply resolved operation
                await _otService.ApplyOperationAsync(itemGuid, result.ResolvedOperation);

                // Broadcast resolution
                var itemGroup = $"item_{itemId}";
                await Clients.Group(itemGroup).SendAsync("ConflictResolved", new
                {
                    ItemId = itemId,
                    ConflictId = conflictId,
                    Resolution = strategy.ToString(),
                    ResolvedOperation = result.ResolvedOperation,
                    ResolvedBy = await GetCurrentUser(userId)
                });

                // Metrics
                await _metricsService.IncrementAsync("conflicts_resolved", 1, new { strategy = strategy.ToString() });

                _logger.LogInformation("Conflict {ConflictId} resolved by user {UserId} with strategy {Strategy}", 
                    conflictId, userId, strategy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict {ConflictId} for item {ItemId}", conflictId, itemId);
            await Clients.Caller.SendAsync("Error", "Erro ao resolver conflito. Tente novamente.");
        }
    }

    // Notificações
    public async Task SendNotification(string workspaceId, NotificationDto notification)
    {
        var userId = GetUserId();
        var workspaceGuid = Guid.Parse(workspaceId);

        // Verificar se é owner/editor para enviar notificações
        if (!await HasWorkspaceAccess(workspaceGuid, userId, PermissionLevel.Editor))
        {
            await Clients.Caller.SendAsync("Error", "Permissão insuficiente");
            return;
        }

        var shardGroup = GetShardGroup(workspaceId);
        await Clients.Group(shardGroup).SendAsync("NotificationReceived", notification);

        _logger.LogInformation("User {UserId} sent notification to workspace {WorkspaceId}", userId, workspaceId);
    }

    // Eventos do ciclo de vida da conexão
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        
        // Metrics
        await _metricsService.IncrementAsync("hub_connections", 1);
        
        _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = GetUserId();
        
        try
        {
            // Criar snapshots para itens em edição
            var activeItems = await _presenceService.GetUserActiveItemsAsync(userId);
            foreach (var itemId in activeItems)
            {
                await _otService.CreateSnapshotIfNeededAsync(Guid.Parse(itemId), userId, SnapshotTrigger.Shutdown);
            }

            // Atualizar presença para offline
            await _presenceService.SetUserOfflineAsync(Context.ConnectionId);

            // Notificar desconexão em todos os grupos que o usuário estava
            var workspaces = await _context.UserPresences
                .Where(p => p.ConnectionId == Context.ConnectionId)
                .Select(p => p.WorkspaceId)
                .ToListAsync();

            foreach (var workspaceId in workspaces)
            {
                var shardGroup = GetShardGroup(workspaceId.ToString());
                await Clients.Group(shardGroup).SendAsync("UserLeft", userId.ToString());
            }

            // Audit log
            await _auditService.LogAsync(AuditAction.Disconnect, "hub", Context.ConnectionId, userId);

            // Metrics
            await _metricsService.IncrementAsync("hub_disconnections", 1);

            _logger.LogInformation("User {UserId} disconnected with connection {ConnectionId}", userId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnection cleanup for user {UserId}", userId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // Métodos auxiliares avançados
    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("id")?.Value;
        return Guid.Parse(userIdClaim);
    }

    private async Task<UserDto> GetCurrentUser(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Avatar = user.Avatar
        };
    }

    private async Task<bool> HasWorkspaceAccess(Guid workspaceId, Guid userId, PermissionLevel minimumLevel = PermissionLevel.Reader)
    {
        var permission = await _context.WorkspacePermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.UserId == userId);

        return permission != null && permission.Level <= minimumLevel;
    }

    private string GetUserColor(Guid userId)
    {
        // Gerar cor consistente baseada no ID do usuário
        var colors = new[] { "#f56a00", "#7265e6", "#ffbf00", "#00a2ae", "#1890ff", "#52c41a", "#eb2f96", "#fa541c" };
        var hash = userId.GetHashCode();
        return colors[Math.Abs(hash) % colors.Length];
    }

    private async Task<UserPlan> GetUserPlanAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.Plan ?? UserPlan.Free;
    }

    private async Task<string> GetSystemParameterAsync(string parameterName)
    {
        var parameter = await _context.SystemParameters
            .FirstOrDefaultAsync(p => p.Key == parameterName);
        
        return parameter?.Value ?? CollaborationParameters.DefaultValues.GetValueOrDefault(parameterName, "0");
    }

    // Sharding Support - Load balancing dinâmico
    private string GetShardGroup(string workspaceId)
    {
        // Implement consistent hashing para distribuir workspaces por shards
        var shardCount = GetShardCount();
        var hash = workspaceId.GetHashCode();
        var shardIndex = Math.Abs(hash) % shardCount;
        
        return $"workspace_{workspaceId}_shard_{shardIndex}";
    }

    private int GetShardCount()
    {
        // Configurável via system parameter ou environment variable
        var shardCountParam = Environment.GetEnvironmentVariable("SIGNALR_SHARD_COUNT");
        return int.TryParse(shardCountParam, out var count) ? count : 1;
    }

    private async Task ProcessMessageMentions(ChatMessageDto message, Guid workspaceId)
    {
        // Extrair menções (@username) da mensagem
        var mentionPattern = @"@(\w+)";
        var mentions = System.Text.RegularExpressions.Regex.Matches(message.Content, mentionPattern);

        foreach (System.Text.RegularExpressions.Match mention in mentions)
        {
            var username = mention.Groups[1].Value;
            
            // Buscar usuário mencionado
            var mentionedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (mentionedUser != null)
            {
                // Verificar se usuário tem acesso ao workspace
                if (await HasWorkspaceAccess(workspaceId, mentionedUser.Id))
                {
                    // Criar notificação de menção
                    await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                    {
                        Title = "Você foi mencionado no chat",
                        Message = $"{message.User.Username} mencionou você: {message.Content.Substring(0, Math.Min(message.Content.Length, 100))}...",
                        Type = NotificationType.ChatMention,
                        Priority = NotificationPriority.High,
                        WorkspaceId = workspaceId,
                        UserIds = new List<Guid> { mentionedUser.Id }
                    });

                    // Metrics
                    await _metricsService.IncrementAsync("chat_mentions", 1);
                }
            }
        }
    }
}
}
```

## 4. Serviços de Tempo Real

### 4.1 Serviço de Presença

#### IDE.Application/Realtime/Services/IUserPresenceService.cs
```csharp
public interface IUserPresenceService
{
    Task SetUserPresenceAsync(Guid workspaceId, Guid userId, string connectionId, UserPresenceStatus status);
    Task SetUserOfflineAsync(string connectionId);
    Task UpdateCurrentItemAsync(Guid workspaceId, Guid userId, string currentItemId);
    Task<List<UserPresenceDto>> GetWorkspacePresenceAsync(Guid workspaceId);
    Task CleanupStaleConnectionsAsync();
}

public class UserPresenceService : IUserPresenceService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public UserPresenceService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task SetUserPresenceAsync(Guid workspaceId, Guid userId, string connectionId, UserPresenceStatus status)
    {
        var existingPresence = await _context.UserPresences
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.UserId == userId);

        if (existingPresence != null)
        {
            existingPresence.ConnectionId = connectionId;
            existingPresence.Status = status;
            existingPresence.LastSeenAt = DateTime.UtcNow;
        }
        else
        {
            var presence = new UserPresence
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                Status = status,
                LastSeenAt = DateTime.UtcNow,
                WorkspaceId = workspaceId,
                UserId = userId
            };

            _context.UserPresences.Add(presence);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SetUserOfflineAsync(string connectionId)
    {
        var presence = await _context.UserPresences
            .FirstOrDefaultAsync(p => p.ConnectionId == connectionId);

        if (presence != null)
        {
            presence.Status = UserPresenceStatus.Offline;
            presence.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateCurrentItemAsync(Guid workspaceId, Guid userId, string currentItemId)
    {
        var presence = await _context.UserPresences
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.UserId == userId);

        if (presence != null)
        {
            presence.CurrentItemId = currentItemId;
            presence.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<UserPresenceDto>> GetWorkspacePresenceAsync(Guid workspaceId)
    {
        var presences = await _context.UserPresences
            .Include(p => p.User)
            .Where(p => p.WorkspaceId == workspaceId && p.Status != UserPresenceStatus.Offline)
            .ToListAsync();

        return _mapper.Map<List<UserPresenceDto>>(presences);
    }

    public async Task<int> GetWorkspaceActiveCountAsync(Guid workspaceId)
    {
        return await _context.UserPresences
            .CountAsync(p => p.WorkspaceId == workspaceId && p.Status != UserPresenceStatus.Offline);
    }

    public async Task<int> GetItemActiveEditorsCountAsync(Guid itemId)
    {
        return await _context.UserPresences
            .CountAsync(p => p.CurrentItemId == itemId.ToString() && p.Status != UserPresenceStatus.Offline);
    }

    public async Task<List<UserPresenceDto>> GetItemActiveEditorsAsync(Guid itemId)
    {
        var presences = await _context.UserPresences
            .Include(p => p.User)
            .Where(p => p.CurrentItemId == itemId.ToString() && p.Status != UserPresenceStatus.Offline)
            .ToListAsync();

        return _mapper.Map<List<UserPresenceDto>>(presences);
    }

    public async Task<List<string>> GetUserActiveItemsAsync(Guid userId)
    {
        var presences = await _context.UserPresences
            .Where(p => p.UserId == userId && !string.IsNullOrEmpty(p.CurrentItemId))
            .Select(p => p.CurrentItemId)
            .ToListAsync();

        return presences;
    }

    public async Task CleanupStaleConnectionsAsync()
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-5);
        
        var stalePresences = await _context.UserPresences
            .Where(p => p.LastSeenAt < staleThreshold && p.Status != UserPresenceStatus.Offline)
            .ToListAsync();

        foreach (var presence in stalePresences)
        {
            presence.Status = UserPresenceStatus.Offline;
            presence.LastSeenAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}

### 4.2 Serviço de Operational Transform

#### IDE.Application/Realtime/Services/IOperationalTransformService.cs
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

public class OperationalTransformService : IOperationalTransformService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<OperationalTransformService> _logger;
    private readonly ISystemParameterService _systemParameterService;

    public OperationalTransformService(
        ApplicationDbContext context, 
        IMapper mapper, 
        ILogger<OperationalTransformService> logger,
        ISystemParameterService systemParameterService)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
        _systemParameterService = systemParameterService;
    }

    public async Task<OperationTransformResult> ProcessOperationAsync(Guid itemId, TextOperationDto operation, Guid userId)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Obter operações concorrentes desde o sequence number da operação
            var concurrentOperations = await _context.TextOperations
                .Where(o => o.ItemId == itemId && 
                           o.SequenceNumber >= operation.SequenceNumber && 
                           o.UserId != userId)
                .OrderBy(o => o.SequenceNumber)
                .ToListAsync();

            var result = new OperationTransformResult
            {
                OriginalOperation = operation,
                TransformedOperation = operation,
                HasConflict = false,
                ConflictType = ConflictType.Simple,
                ProcessingTime = DateTime.UtcNow - startTime,
                SequenceNumber = await GetNextSequenceNumberAsync(itemId)
            };

            if (concurrentOperations.Any())
            {
                // Detectar e classificar conflitos
                var conflictAnalysis = AnalyzeConflicts(operation, concurrentOperations);
                result.HasConflict = conflictAnalysis.HasConflict;
                result.ConflictType = conflictAnalysis.ConflictType;

                if (result.HasConflict)
                {
                    if (conflictAnalysis.ConflictType == ConflictType.Simple)
                    {
                        // Transform automaticamente conflitos simples
                        result.TransformedOperation = await TransformOperation(operation, concurrentOperations);
                        result.HasConflict = false; // Resolvido automaticamente
                    }
                    else
                    {
                        // Conflitos complexos ou críticos requerem intervenção
                        result.ConflictDescription = conflictAnalysis.Description;
                        result.ConflictingOperations = _mapper.Map<List<TextOperationDto>>(concurrentOperations);
                        result.ResolutionOptions = GenerateResolutionOptions(operation, concurrentOperations);
                    }
                }
            }

            // Persistir operação se não houver conflito
            if (!result.HasConflict)
            {
                await PersistOperationAsync(itemId, result.TransformedOperation, userId, result.SequenceNumber);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing operation for item {ItemId}", itemId);
            throw;
        }
    }

    public async Task ApplyOperationAsync(Guid itemId, TextOperationDto operation)
    {
        var item = await _context.ModuleItems.FindAsync(itemId);
        if (item == null) return;

        // Aplicar operação usando algoritmo Google Wave OT
        item.Content = ApplyTextOperation(item.Content, operation);
        item.UpdatedAt = DateTime.UtcNow;
        item.VersionNumber++;

        await _context.SaveChangesAsync();
    }

    public async Task<CollaborationSnapshotDto> CreateSnapshotIfNeededAsync(Guid itemId, Guid userId, SnapshotTrigger trigger)
    {
        var shouldCreate = await ShouldCreateSnapshotAsync(itemId, trigger);
        
        if (shouldCreate)
        {
            return await CreateSnapshotAsync(itemId, userId, trigger);
        }

        return null;
    }

    public async Task<CollaborationSnapshotDto> CreateSnapshotAsync(Guid itemId, Guid userId, SnapshotTrigger trigger)
    {
        var item = await _context.ModuleItems.FindAsync(itemId);
        if (item == null) return null;

        // Contar operações desde último snapshot
        var lastSnapshot = await GetLatestSnapshotAsync(itemId);
        var operationCount = await _context.TextOperations
            .CountAsync(o => o.ItemId == itemId && 
                           (lastSnapshot == null || o.Timestamp > DateTime.Parse(lastSnapshot.CreatedAt.ToString())));

        var snapshot = new CollaborationSnapshot
        {
            Id = Guid.NewGuid(),
            Content = item.Content,
            OperationCount = operationCount,
            Trigger = trigger,
            CreatedAt = DateTime.UtcNow,
            ContentSize = item.Content?.Length ?? 0,
            ContentHash = ComputeContentHash(item.Content),
            ItemId = itemId,
            CreatedByUserId = userId
        };

        _context.CollaborationSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // Cleanup snapshots antigos
        await CleanupOldSnapshotsAsync(itemId);

        return _mapper.Map<CollaborationSnapshotDto>(snapshot);
    }

    // Google Wave OT Implementation
    private string ApplyTextOperation(string content, TextOperationDto operation)
    {
        switch (operation.Type)
        {
            case OperationType.Insert:
                return content.Insert(operation.Position, operation.Content);
                
            case OperationType.Delete:
                var deleteLength = Math.Min(operation.Length, content.Length - operation.Position);
                return content.Remove(operation.Position, deleteLength);
                
            case OperationType.Retain:
                return content; // Retain não modifica o conteúdo
                
            default:
                return content;
        }
    }

    private async Task<TextOperationDto> TransformOperation(TextOperationDto operation, List<TextOperation> concurrentOps)
    {
        var transformed = operation;

        foreach (var concurrentOp in concurrentOps)
        {
            transformed = TransformTwoOperations(transformed, _mapper.Map<TextOperationDto>(concurrentOp));
        }

        return transformed;
    }

    private TextOperationDto TransformTwoOperations(TextOperationDto op1, TextOperationDto op2)
    {
        // Implementação simplificada do algoritmo Google Wave OT
        if (op1.Type == OperationType.Insert && op2.Type == OperationType.Insert)
        {
            if (op1.Position <= op2.Position)
            {
                return op1; // op1 não precisa ser transformada
            }
            else
            {
                op1.Position += op2.Content.Length; // Ajustar posição
                return op1;
            }
        }
        else if (op1.Type == OperationType.Insert && op2.Type == OperationType.Delete)
        {
            if (op1.Position <= op2.Position)
            {
                return op1;
            }
            else if (op1.Position > op2.Position + op2.Length)
            {
                op1.Position -= op2.Length;
                return op1;
            }
            else
            {
                op1.Position = op2.Position;
                return op1;
            }
        }
        else if (op1.Type == OperationType.Delete && op2.Type == OperationType.Insert)
        {
            if (op1.Position < op2.Position)
            {
                return op1;
            }
            else
            {
                op1.Position += op2.Content.Length;
                return op1;
            }
        }
        else if (op1.Type == OperationType.Delete && op2.Type == OperationType.Delete)
        {
            if (op1.Position + op1.Length <= op2.Position)
            {
                return op1;
            }
            else if (op1.Position >= op2.Position + op2.Length)
            {
                op1.Position -= op2.Length;
                return op1;
            }
            else
            {
                // Overlapping deletes - conflict resolution needed
                return op1; // Simplificado
            }
        }

        return op1;
    }

    private ConflictAnalysis AnalyzeConflicts(TextOperationDto operation, List<TextOperation> concurrentOps)
    {
        var analysis = new ConflictAnalysis
        {
            HasConflict = false,
            ConflictType = ConflictType.Simple,
            Description = ""
        };

        foreach (var concurrentOp in concurrentOps)
        {
            var concurrentDto = _mapper.Map<TextOperationDto>(concurrentOp);
            
            if (IsOperationOverlapping(operation, concurrentDto))
            {
                analysis.HasConflict = true;
                
                if (IsComplexConflict(operation, concurrentDto))
                {
                    analysis.ConflictType = ConflictType.Complex;
                    analysis.Description = "Operações sobrepostas requerem escolha do usuário";
                }
                else if (IsCriticalConflict(operation, concurrentDto))
                {
                    analysis.ConflictType = ConflictType.Critical;
                    analysis.Description = "Conflito crítico requer revisão manual";
                }
            }
        }

        return analysis;
    }

    private bool IsOperationOverlapping(TextOperationDto op1, TextOperationDto op2)
    {
        if (op1.Type == OperationType.Delete && op2.Type == OperationType.Delete)
        {
            return !(op1.Position + op1.Length <= op2.Position || op2.Position + op2.Length <= op1.Position);
        }
        
        return Math.Abs(op1.Position - op2.Position) < 10; // Threshold configurável
    }

    private bool IsComplexConflict(TextOperationDto op1, TextOperationDto op2)
    {
        return op1.Type == OperationType.Delete && op2.Type == OperationType.Delete && IsOperationOverlapping(op1, op2);
    }

    private bool IsCriticalConflict(TextOperationDto op1, TextOperationDto op2)
    {
        // Conflito crítico se envolve mudanças em estruturas importantes
        return op1.Content?.Contains("function") == true || op2.Content?.Contains("function") == true;
    }

    // Métodos auxiliares
    private async Task<bool> ShouldCreateSnapshotAsync(Guid itemId, SnapshotTrigger trigger)
    {
        switch (trigger)
        {
            case SnapshotTrigger.OperationCount:
                var maxOps = await _systemParameterService.GetIntAsync(
                    CollaborationParameters.COLLABORATION_SNAPSHOT_EVERY_EDITS);
                var opCount = await GetOperationCountSinceLastSnapshotAsync(itemId);
                return opCount >= maxOps;

            case SnapshotTrigger.TimeInterval:
                var maxMinutes = await _systemParameterService.GetIntAsync(
                    CollaborationParameters.COLLABORATION_SNAPSHOT_EVERY_MINUTES);
                var lastSnapshot = await GetLatestSnapshotAsync(itemId);
                var timeSinceSnapshot = DateTime.UtcNow - (lastSnapshot?.CreatedAt ?? DateTime.UtcNow.AddHours(-1));
                return timeSinceSnapshot.TotalMinutes >= maxMinutes;

            case SnapshotTrigger.Manual:
            case SnapshotTrigger.Conflict:
            case SnapshotTrigger.Shutdown:
                return true;

            default:
                return false;
        }
    }

    private async Task<int> GetOperationCountSinceLastSnapshotAsync(Guid itemId)
    {
        var lastSnapshot = await GetLatestSnapshotAsync(itemId);
        var cutoffTime = lastSnapshot?.CreatedAt ?? DateTime.MinValue;

        return await _context.TextOperations
            .CountAsync(o => o.ItemId == itemId && o.Timestamp > cutoffTime);
    }

    private string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content ?? ""));
        return Convert.ToBase64String(hash);
    }

    private async Task CleanupOldSnapshotsAsync(Guid itemId)
    {
        var maxSnapshots = await _systemParameterService.GetIntAsync(
            CollaborationParameters.COLLABORATION_MAX_SNAPSHOTS_PER_ITEM);
        
        var retentionDays = await _systemParameterService.GetIntAsync(
            CollaborationParameters.COLLABORATION_SNAPSHOT_RETENTION_DAYS);

        // Remove snapshots by count
        var snapshots = await _context.CollaborationSnapshots
            .Where(s => s.ItemId == itemId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip(maxSnapshots)
            .ToListAsync();

        // Remove snapshots by age
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var oldSnapshots = await _context.CollaborationSnapshots
            .Where(s => s.ItemId == itemId && s.CreatedAt < cutoffDate)
            .ToListAsync();

        var toRemove = snapshots.Union(oldSnapshots).Distinct();
        _context.CollaborationSnapshots.RemoveRange(toRemove);
        
        if (toRemove.Any())
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task<int> GetNextSequenceNumberAsync(Guid itemId)
    {
        var lastSequence = await _context.TextOperations
            .Where(o => o.ItemId == itemId)
            .MaxAsync(o => (int?)o.SequenceNumber) ?? 0;
        
        return lastSequence + 1;
    }

    private async Task PersistOperationAsync(Guid itemId, TextOperationDto operation, Guid userId, int sequenceNumber)
    {
        var textOperation = new TextOperation
        {
            Id = Guid.NewGuid(),
            Type = operation.Type,
            Position = operation.Position,
            Length = operation.Length,
            Content = operation.Content,
            Timestamp = DateTime.UtcNow,
            ClientId = operation.ClientId,
            SequenceNumber = sequenceNumber,
            ItemId = itemId,
            UserId = userId
        };

        _context.TextOperations.Add(textOperation);
        await _context.SaveChangesAsync();
    }

    private List<TextOperationDto> GenerateResolutionOptions(TextOperationDto operation, List<TextOperation> conflictingOps)
    {
        var options = new List<TextOperationDto>();
        
        // Opção 1: Manter operação original
        options.Add(operation);
        
        // Opção 2: Aplicar operações conflitantes primeiro
        foreach (var conflictOp in conflictingOps)
        {
            options.Add(_mapper.Map<TextOperationDto>(conflictOp));
        }

        return options;
    }
}
}
```

### 4.2 Serviço de Chat

#### IDE.Application/Realtime/Services/IChatService.cs
```csharp
public interface IChatService
{
    Task<ChatMessageDto> SendMessageAsync(Guid workspaceId, Guid userId, SendChatMessageRequest request);
    Task<ChatMessageDto> EditMessageAsync(Guid messageId, Guid userId, EditChatMessageRequest request);
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
    Task<List<ChatMessageDto>> GetWorkspaceChatHistoryAsync(Guid workspaceId, Guid userId, int page = 1, int pageSize = 50);
    Task<List<ChatMessageDto>> GetMessageRepliesAsync(Guid messageId, Guid userId);
}

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IWorkspaceService _workspaceService;

    public ChatService(ApplicationDbContext context, IMapper mapper, IWorkspaceService workspaceService)
    {
        _context = context;
        _mapper = mapper;
        _workspaceService = workspaceService;
    }

    public async Task<ChatMessageDto> SendMessageAsync(Guid workspaceId, Guid userId, SendChatMessageRequest request)
    {
        // Verificar acesso ao workspace
        if (!await _workspaceService.HasWorkspaceAccessAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Acesso negado ao workspace");
        }

        // Criar mensagem
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            Content = request.Content,
            Type = request.Type,
            CreatedAt = DateTime.UtcNow,
            WorkspaceId = workspaceId,
            UserId = userId,
            ParentMessageId = request.ParentMessageId
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // Retornar DTO com dados do usuário
        var messageWithUser = await _context.ChatMessages
            .Include(m => m.User)
            .Include(m => m.Replies)
            .FirstAsync(m => m.Id == message.Id);

        return _mapper.Map<ChatMessageDto>(messageWithUser);
    }

    public async Task<ChatMessageDto> EditMessageAsync(Guid messageId, Guid userId, EditChatMessageRequest request)
    {
        var message = await _context.ChatMessages
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            throw new KeyNotFoundException("Mensagem não encontrada");
        }

        // Verificar se é o autor da mensagem
        if (message.UserId != userId)
        {
            throw new UnauthorizedAccessException("Apenas o autor pode editar a mensagem");
        }

        // Verificar se a mensagem não é muito antiga (ex: 24 horas)
        if (DateTime.UtcNow.Subtract(message.CreatedAt).TotalHours > 24)
        {
            throw new InvalidOperationException("Mensagens antigas não podem ser editadas");
        }

        message.Content = request.Content;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return _mapper.Map<ChatMessageDto>(message);
    }

    public async Task<List<ChatMessageDto>> GetWorkspaceChatHistoryAsync(Guid workspaceId, Guid userId, int page = 1, int pageSize = 50)
    {
        // Verificar acesso
        if (!await _workspaceService.HasWorkspaceAccessAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Acesso negado ao workspace");
        }

        var messages = await _context.ChatMessages
            .Include(m => m.User)
            .Include(m => m.Replies).ThenInclude(r => r.User)
            .Where(m => m.WorkspaceId == workspaceId && m.ParentMessageId == null)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return _mapper.Map<List<ChatMessageDto>>(messages.OrderBy(m => m.CreatedAt));
    }

    // ... outros métodos
}
```

### 4.3 Serviço de Notificações

#### IDE.Application/Realtime/Services/INotificationService.cs
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

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IHubContext<WorkspaceHub> _hubContext;

    public NotificationService(ApplicationDbContext context, IMapper mapper, IHubContext<WorkspaceHub> hubContext)
    {
        _context = context;
        _mapper = mapper;
        _hubContext = hubContext;
    }

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request)
    {
        var notifications = new List<Notification>();

        foreach (var userId in request.UserIds)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Message = request.Message,
                Type = request.Type,
                Priority = request.Priority,
                ActionUrl = request.ActionUrl,
                ActionData = request.ActionData,
                WorkspaceId = request.WorkspaceId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            notifications.Add(notification);
            _context.Notifications.Add(notification);
        }

        await _context.SaveChangesAsync();

        // Enviar notificação em tempo real para os usuários
        var notificationDto = _mapper.Map<NotificationDto>(notifications.First());
        
        foreach (var userId in request.UserIds)
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("NotificationReceived", notificationDto);
        }

        return notificationDto;
    }

    public async Task SendWorkspaceNotificationAsync(Guid workspaceId, NotificationType type, string title, string message, Guid? triggeredById = null)
    {
        // Obter todos os usuários do workspace
        var userIds = await _context.WorkspacePermissions
            .Where(p => p.WorkspaceId == workspaceId)
            .Select(p => p.UserId)
            .ToListAsync();

        // Remover o usuário que triggou a notificação
        if (triggeredById.HasValue)
        {
            userIds.Remove(triggeredById.Value);
        }

        if (userIds.Any())
        {
            var request = new CreateNotificationRequest
            {
                Title = title,
                Message = message,
                Type = type,
                WorkspaceId = workspaceId,
                UserIds = userIds
            };

            await CreateNotificationAsync(request);
        }
    }

    // ... outros métodos
}
```

## 5. Endpoints de Tempo Real

### IDE.API/Endpoints/RealtimeEndpoints.cs
```csharp
public static class RealtimeEndpoints
{
    public static void MapRealtimeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/realtime")
            .WithTags("Real-time")
            .WithOpenApi()
            .RequireAuthorization();

        // Chat endpoints
        group.MapGet("/workspaces/{workspaceId:guid}/chat", async (
            [FromRoute] Guid workspaceId,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] IChatService chatService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var messages = await chatService.GetWorkspaceChatHistoryAsync(workspaceId, userId, page, pageSize);
            
            return Results.Ok(ApiResponse<List<ChatMessageDto>>.Success(messages, "Histórico de chat obtido com sucesso"));
        })
        .WithName("GetChatHistory")
        .WithSummary("Obter histórico de chat do workspace");

        // Notifications endpoints
        group.MapGet("/notifications", async (
            [FromQuery] bool unreadOnly,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] INotificationService notificationService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var notifications = await notificationService.GetUserNotificationsAsync(userId, unreadOnly, page, pageSize);
            
            return Results.Ok(ApiResponse<List<NotificationDto>>.Success(notifications, "Notificações obtidas com sucesso"));
        })
        .WithName("GetNotifications")
        .WithSummary("Obter notificações do usuário");

        group.MapPost("/notifications/{notificationId:guid}/read", async (
            [FromRoute] Guid notificationId,
            [FromServices] INotificationService notificationService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var success = await notificationService.MarkAsReadAsync(notificationId, userId);
            
            if (!success)
            {
                return Results.NotFound(ApiResponse<object>.Error("Notificação não encontrada"));
            }

            return Results.Ok(ApiResponse<object>.Success(null, "Notificação marcada como lida"));
        })
        .WithName("MarkNotificationAsRead")
        .WithSummary("Marcar notificação como lida");

        // Presence endpoints
        group.MapGet("/workspaces/{workspaceId:guid}/presence", async (
            [FromRoute] Guid workspaceId,
            [FromServices] IUserPresenceService presenceService,
            ClaimsPrincipal user) =>
        {
            var presences = await presenceService.GetWorkspacePresenceAsync(workspaceId);
            
            return Results.Ok(ApiResponse<List<UserPresenceDto>>.Success(presences, "Presença obtida com sucesso"));
        })
        .WithName("GetWorkspacePresence")
        .WithSummary("Obter usuários presentes no workspace");
    }
}
```

## 6. Configuração do SignalR

### IDE.API/Program.cs (Adições)
```csharp
// ... configurações existentes ...

// Configuração do SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Serviços de tempo real
builder.Services.AddScoped<IUserPresenceService, UserPresenceService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ... resto da configuração ...

var app = builder.Build();

// ... middleware existente ...

// Configurar hub SignalR
app.MapHub<WorkspaceHub>("/hubs/workspace");

// Endpoints
app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapRealtimeEndpoints(); // Novo

// ... resto da configuração ...
```

## Entregáveis da Fase 3

✅ **SignalR Hub** completo com autenticação  
✅ **Edição colaborativa** com tracking de mudanças  
✅ **Sistema de chat** com histórico e replies  
✅ **Notificações em tempo real** por workspace  
✅ **Gestão de presença** de usuários ativos  
✅ **Cursores múltiplos** para edição colaborativa  
✅ **Indicadores de digitação** no chat  
✅ **Endpoints REST** para histórico e configurações  

## Validação da Fase 3

### Critérios de Sucesso
- [ ] Usuários conseguem se conectar ao hub SignalR
- [ ] Edição colaborativa funciona com múltiplos usuários
- [ ] Chat em tempo real funciona corretamente
- [ ] Notificações são entregues em tempo real
- [ ] Presença de usuários é atualizada automaticamente
- [ ] Cursores de outros usuários são visíveis
- [ ] Conexões são limpas adequadamente ao desconectar
- [ ] Permissões de workspace são respeitadas no hub
- [ ] Histórico de chat é persistido corretamente

### Testes Manuais
```javascript
// Conectar ao hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/workspace", {
        accessTokenFactory: () => localStorage.getItem("accessToken")
    })
    .build();

// Entrar em workspace
await connection.invoke("JoinWorkspace", "workspace-id");

// Editar item colaborativamente
await connection.invoke("JoinItem", "item-id");
await connection.invoke("SendEdit", "item-id", {
    type: "insert",
    startLine: 1,
    startColumn: 0,
    content: "Hello World"
});

// Enviar mensagem no chat
await connection.invoke("SendMessage", "workspace-id", {
    content: "Olá pessoal!",
    type: "Text"
});
```

### 4.3 Serviço de Métricas e Monitoramento

#### IDE.Application/Realtime/Services/ICollaborationMetricsService.cs
```csharp
public interface ICollaborationMetricsService
{
    Task IncrementAsync(string metricName, double value, object tags = null);
    Task RecordLatency(string metricName, TimeSpan latency, object tags = null);
    Task SetGaugeAsync(string metricName, double value, object tags = null);
    Task<List<CollaborationMetrics>> GetMetricsAsync(string metricName, DateTime from, DateTime to, Guid? workspaceId = null);
    Task<CollaborationDashboardData> GetDashboardDataAsync(Guid? workspaceId = null);
    Task CleanupOldMetricsAsync();
}

public class CollaborationMetricsService : ICollaborationMetricsService
{
    private readonly ApplicationDbContext _context;
    private readonly ISystemParameterService _systemParameterService;
    private readonly ILogger<CollaborationMetricsService> _logger;

    public CollaborationMetricsService(
        ApplicationDbContext context,
        ISystemParameterService systemParameterService,
        ILogger<CollaborationMetricsService> logger)
    {
        _context = context;
        _systemParameterService = systemParameterService;
        _logger = logger;
    }

    public async Task IncrementAsync(string metricName, double value, object tags = null)
    {
        var metric = new CollaborationMetrics
        {
            Id = Guid.NewGuid(),
            Type = MetricType.Counter,
            MetricName = metricName,
            Value = value,
            Unit = "count",
            Tags = tags != null ? JsonSerializer.Serialize(tags) : null,
            Timestamp = DateTime.UtcNow
        };

        if (tags != null && HasWorkspaceId(tags, out var workspaceId))
        {
            metric.WorkspaceId = workspaceId;
        }

        _context.CollaborationMetrics.Add(metric);
        await _context.SaveChangesAsync();
    }

    public async Task RecordLatency(string metricName, TimeSpan latency, object tags = null)
    {
        var metric = new CollaborationMetrics
        {
            Id = Guid.NewGuid(),
            Type = MetricType.Timer,
            MetricName = metricName,
            Value = latency.TotalMilliseconds,
            Unit = "ms",
            Tags = tags != null ? JsonSerializer.Serialize(tags) : null,
            Timestamp = DateTime.UtcNow
        };

        _context.CollaborationMetrics.Add(metric);
        await _context.SaveChangesAsync();
    }

    public async Task SetGaugeAsync(string metricName, double value, object tags = null)
    {
        var metric = new CollaborationMetrics
        {
            Id = Guid.NewGuid(),
            Type = MetricType.Gauge,
            MetricName = metricName,
            Value = value,
            Unit = "value",
            Tags = tags != null ? JsonSerializer.Serialize(tags) : null,
            Timestamp = DateTime.UtcNow
        };

        _context.CollaborationMetrics.Add(metric);
        await _context.SaveChangesAsync();
    }

    public async Task<CollaborationDashboardData> GetDashboardDataAsync(Guid? workspaceId = null)
    {
        var timeWindow = DateTime.UtcNow.AddHours(-24);
        
        var query = _context.CollaborationMetrics
            .Where(m => m.Timestamp >= timeWindow);

        if (workspaceId.HasValue)
        {
            query = query.Where(m => m.WorkspaceId == workspaceId);
        }

        var metrics = await query.ToListAsync();

        return new CollaborationDashboardData
        {
            // Dashboard de Colaboração
            ActiveUsers = await GetActiveUsersCount(workspaceId),
            TotalEdits = GetMetricSum(metrics, "edit_operations"),
            AverageLatency = GetMetricAverage(metrics, "edit_latency"),
            ConflictsResolved = GetMetricSum(metrics, "conflicts_resolved"),
            ChatMessages = GetMetricSum(metrics, "chat_messages"),
            
            // Dashboard de Performance
            SignalRConnections = await GetActiveConnectionsCount(),
            RedisMemoryUsage = await GetRedisMemoryUsage(),
            OperationThroughput = GetMetricRate(metrics, "edit_operations"),
            AverageConflictResolutionTime = GetMetricAverage(metrics, "conflict_resolution_time"),
            ReconnectionRate = GetMetricRate(metrics, "hub_disconnections"),
            
            // Dashboard de Sistema
            RateLimitViolations = GetMetricSum(metrics, "rate_limit_violations"),
            AverageWorkspaceSize = await GetAverageWorkspaceSize(),
            UserDistribution = await GetUserDistribution(workspaceId),
            SystemHealth = await GetSystemHealthStatus(),
            
            GeneratedAt = DateTime.UtcNow
        };
    }

    private bool HasWorkspaceId(object tags, out Guid workspaceId)
    {
        workspaceId = Guid.Empty;
        
        if (tags == null) return false;
        
        var tagsJson = JsonSerializer.Serialize(tags);
        var tagsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(tagsJson);
        
        if (tagsDict?.ContainsKey("workspaceId") == true)
        {
            return Guid.TryParse(tagsDict["workspaceId"].ToString(), out workspaceId);
        }
        
        return false;
    }

    private async Task<int> GetActiveUsersCount(Guid? workspaceId)
    {
        var query = _context.UserPresences
            .Where(p => p.Status != UserPresenceStatus.Offline);

        if (workspaceId.HasValue)
        {
            query = query.Where(p => p.WorkspaceId == workspaceId);
        }

        return await query.CountAsync();
    }

    private double GetMetricSum(List<CollaborationMetrics> metrics, string metricName)
    {
        return metrics.Where(m => m.MetricName == metricName).Sum(m => m.Value);
    }

    private double GetMetricAverage(List<CollaborationMetrics> metrics, string metricName)
    {
        var relevantMetrics = metrics.Where(m => m.MetricName == metricName).ToList();
        return relevantMetrics.Any() ? relevantMetrics.Average(m => m.Value) : 0;
    }

    private double GetMetricRate(List<CollaborationMetrics> metrics, string metricName)
    {
        var relevantMetrics = metrics.Where(m => m.MetricName == metricName)
            .OrderBy(m => m.Timestamp)
            .ToList();

        if (relevantMetrics.Count < 2) return 0;

        var timespan = relevantMetrics.Last().Timestamp - relevantMetrics.First().Timestamp;
        var totalValue = relevantMetrics.Sum(m => m.Value);
        
        return totalValue / Math.Max(timespan.TotalHours, 1);
    }
}
```

### 4.4 Serviço de Rate Limiting

#### IDE.Application/Realtime/Services/IRateLimitingService.cs
```csharp
public interface IRateLimitingService
{
    Task<bool> CheckLimitAsync(Guid userId, string operation);
    Task<bool> CheckEditLimitAsync(Guid userId, UserPlan userPlan);
    Task<bool> CheckChatLimitAsync(Guid userId, UserPlan userPlan);
    Task<bool> CheckCursorLimitAsync(Guid userId);
    Task<bool> CheckPresenceLimitAsync(Guid userId);
    Task<RateLimitStatus> GetUserLimitStatusAsync(Guid userId);
    Task ResetUserLimitsAsync(Guid userId);
}

public class RateLimitingService : IRateLimitingService
{
    private readonly IMemoryCache _cache;
    private readonly ISystemParameterService _systemParameterService;
    private readonly ICollaborationMetricsService _metricsService;
    private readonly ILogger<RateLimitingService> _logger;

    public RateLimitingService(
        IMemoryCache cache,
        ISystemParameterService systemParameterService,
        ICollaborationMetricsService metricsService,
        ILogger<RateLimitingService> logger)
    {
        _cache = cache;
        _systemParameterService = systemParameterService;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<bool> CheckEditLimitAsync(Guid userId, UserPlan userPlan)
    {
        var limitKey = GetPlanLimitKey(userPlan, "edits_per_minute");
        var limit = await _systemParameterService.GetIntAsync(limitKey);
        
        return await CheckRateLimitAsync(userId, "edit", limit, TimeSpan.FromMinutes(1));
    }

    public async Task<bool> CheckChatLimitAsync(Guid userId, UserPlan userPlan)
    {
        var limitKey = GetPlanLimitKey(userPlan, "chat_per_minute");
        var limit = await _systemParameterService.GetIntAsync(limitKey);
        
        return await CheckRateLimitAsync(userId, "chat", limit, TimeSpan.FromMinutes(1));
    }

    public async Task<bool> CheckCursorLimitAsync(Guid userId)
    {
        var limit = await _systemParameterService.GetIntAsync(
            CollaborationParameters.COLLABORATION_RATE_LIMIT_CURSOR_OPERATION);
        
        return await CheckRateLimitAsync(userId, "cursor", limit, TimeSpan.FromSeconds(1));
    }

    public async Task<bool> CheckPresenceLimitAsync(Guid userId)
    {
        var limit = await _systemParameterService.GetIntAsync(
            CollaborationParameters.COLLABORATION_RATE_LIMIT_PRESENCE_OPERATION);
        
        return await CheckRateLimitAsync(userId, "presence", limit, TimeSpan.FromMinutes(1));
    }

    public async Task<bool> CheckLimitAsync(Guid userId, string operation)
    {
        // Limits gerais por operação
        var limitKey = $"collaboration.rate_limit.operation.{operation}_per_minute";
        var limit = await _systemParameterService.GetIntAsync(limitKey);
        
        return await CheckRateLimitAsync(userId, operation, limit, TimeSpan.FromMinutes(1));
    }

    private async Task<bool> CheckRateLimitAsync(Guid userId, string operation, int limit, TimeSpan window)
    {
        var cacheKey = $"rate_limit_{userId}_{operation}";
        var now = DateTime.UtcNow;
        
        if (_cache.TryGetValue(cacheKey, out List<DateTime> requests))
        {
            // Remove requests outside the time window
            requests.RemoveAll(r => now - r > window);
            
            if (requests.Count >= limit)
            {
                // Rate limit exceeded
                await _metricsService.IncrementAsync("rate_limit_violations", 1, new { userId = userId.ToString(), operation });
                _logger.LogWarning("Rate limit exceeded for user {UserId} operation {Operation}", userId, operation);
                return false;
            }
            
            requests.Add(now);
        }
        else
        {
            requests = new List<DateTime> { now };
        }
        
        _cache.Set(cacheKey, requests, window);
        return true;
    }

    private string GetPlanLimitKey(UserPlan userPlan, string operationType)
    {
        var planName = userPlan.ToString().ToLower();
        return $"collaboration.rate_limit.{planName}.{operationType}";
    }

    public async Task<RateLimitStatus> GetUserLimitStatusAsync(Guid userId)
    {
        // Implementation for getting current rate limit status
        return new RateLimitStatus
        {
            UserId = userId,
            EditRequests = GetCurrentRequestCount(userId, "edit"),
            ChatRequests = GetCurrentRequestCount(userId, "chat"),
            CursorRequests = GetCurrentRequestCount(userId, "cursor"),
            PresenceRequests = GetCurrentRequestCount(userId, "presence"),
            LastReset = DateTime.UtcNow.Date
        };
    }

    private int GetCurrentRequestCount(Guid userId, string operation)
    {
        var cacheKey = $"rate_limit_{userId}_{operation}";
        
        if (_cache.TryGetValue(cacheKey, out List<DateTime> requests))
        {
            return requests.Count;
        }
        
        return 0;
    }
}
```

### 4.5 Serviço de Auditoria

#### IDE.Application/Realtime/Services/ICollaborationAuditService.cs
```csharp
public interface ICollaborationAuditService
{
    Task LogAsync(AuditAction action, string resource, string resourceId, Guid userId, string details = null);
    Task<List<CollaborationAuditLog>> GetAuditLogsAsync(Guid? workspaceId, DateTime? from, DateTime? to, int page = 1, int pageSize = 50);
    Task<List<CollaborationAuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? from, DateTime? to, int page = 1, int pageSize = 50);
    Task CleanupOldAuditLogsAsync();
}

public class CollaborationAuditService : ICollaborationAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISystemParameterService _systemParameterService;
    private readonly ILogger<CollaborationAuditService> _logger;

    public CollaborationAuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ISystemParameterService systemParameterService,
        ILogger<CollaborationAuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _systemParameterService = systemParameterService;
        _logger = logger;
    }

    public async Task LogAsync(AuditAction action, string resource, string resourceId, Guid userId, string details = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            var auditLog = new CollaborationAuditLog
            {
                Id = Guid.NewGuid(),
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                Details = details,
                IpAddress = GetClientIpAddress(httpContext),
                UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                UserId = userId
            };

            // Set workspace ID if resource is workspace-related
            if (resource == "workspace" && Guid.TryParse(resourceId, out var workspaceId))
            {
                auditLog.WorkspaceId = workspaceId;
            }
            else if (resource == "item")
            {
                var item = await _context.ModuleItems.FindAsync(Guid.Parse(resourceId));
                auditLog.WorkspaceId = item?.WorkspaceId;
            }

            _context.CollaborationAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit action {Action} for user {UserId}", action, userId);
        }
    }

    public async Task<List<CollaborationAuditLog>> GetAuditLogsAsync(Guid? workspaceId, DateTime? from, DateTime? to, int page = 1, int pageSize = 50)
    {
        var query = _context.CollaborationAuditLogs.AsQueryable();

        if (workspaceId.HasValue)
        {
            query = query.Where(log => log.WorkspaceId == workspaceId);
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.Timestamp >= from);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.Timestamp <= to);
        }

        return await query
            .OrderByDescending(log => log.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(log => log.User)
            .ToListAsync();
    }

    public async Task CleanupOldAuditLogsAsync()
    {
        var retentionDays = await _systemParameterService.GetIntAsync(
            CollaborationParameters.COLLABORATION_AUDIT_RETENTION_DAYS);
        
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        var oldLogs = await _context.CollaborationAuditLogs
            .Where(log => log.Timestamp < cutoffDate)
            .ToListAsync();

        if (oldLogs.Any())
        {
            _context.CollaborationAuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Cleaned up {Count} old audit logs", oldLogs.Count);
        }
    }

    private string GetClientIpAddress(HttpContext httpContext)
    {
        if (httpContext == null) return "unknown";

        var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        }
        
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        }
        
        return ipAddress ?? "unknown";
    }
}
```

## 5. DTOs e Enums para Comunicação em Tempo Real

### 5.1 DTOs de Operações de Texto

#### IDE.Application/Realtime/DTOs/OperationDTOs.cs
```csharp
public class TextOperationDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public OperationType Type { get; set; }
    public int Position { get; set; }
    public string Content { get; set; }
    public int? Length { get; set; }
    public long SequenceNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ClientId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class TransformOperationResultDto
{
    public TextOperationDto TransformedOperation { get; set; }
    public List<ConflictDto> Conflicts { get; set; } = new();
    public bool RequiresManualResolution { get; set; }
    public ConflictResolutionStrategy SuggestedStrategy { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class OperationBatchDto
{
    public List<TextOperationDto> Operations { get; set; } = new();
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string BatchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalOperations => Operations.Count;
}

public class OperationValidationDto
{
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public TextOperationDto? CorrectedOperation { get; set; }
    public ValidationSeverity Severity { get; set; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
```

### 5.2 DTOs de Conflitos e Resolução

#### IDE.Application/Realtime/DTOs/ConflictDTOs.cs
```csharp
public class ConflictDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WorkspaceId { get; set; }
    public ConflictType Type { get; set; }
    public TextOperationDto LocalOperation { get; set; }
    public TextOperationDto RemoteOperation { get; set; }
    public ConflictSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Description { get; set; }
    public List<ConflictResolutionOptionDto> ResolutionOptions { get; set; } = new();
}

public class ConflictResolutionDto
{
    public Guid ConflictId { get; set; }
    public ConflictResolutionStrategy Strategy { get; set; }
    public Guid ResolvedByUserId { get; set; }
    public TextOperationDto? ResolvedOperation { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime ResolvedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ConflictResolutionOptionDto
{
    public ConflictResolutionStrategy Strategy { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TextOperationDto? PreviewOperation { get; set; }
    public double Confidence { get; set; } // 0.0 to 1.0
    public bool IsRecommended { get; set; }
    public string? Warning { get; set; }
}

public enum ConflictType
{
    ContentOverlap,
    PositionMismatch,
    DeletedContent,
    ConcurrentInsert,
    ConcurrentDelete,
    ConcurrentMove,
    SequenceViolation,
    TimestampConflict
}

public enum ConflictSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ConflictResolutionStrategy
{
    AutoMerge,
    PreferLocal,
    PreferRemote,
    PreferLatest,
    PreferEarliest,
    ManualResolution,
    CreateBranch,
    Reject
}
```

### 5.3 DTOs de Presença e Chat

#### IDE.Application/Realtime/DTOs/PresenceDTOs.cs
```csharp
public class UserPresenceDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string DisplayName { get; set; }
    public string? Avatar { get; set; }
    public UserPresenceStatus Status { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? CurrentItemId { get; set; }
    public DateTime LastSeen { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class UserCursorDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string UserColor { get; set; }
    public Guid ItemId { get; set; }
    public int Position { get; set; }
    public int? SelectionStart { get; set; }
    public int? SelectionEnd { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsActive { get; set; }
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Content { get; set; }
    public ChatMessageType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ReplyToId { get; set; }
    public Guid? ItemId { get; set; } // Para mensagens relacionadas a itens específicos
    public List<ChatAttachmentDto>? Attachments { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ChatAttachmentDto
{
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public string Url { get; set; }
    public string? Thumbnail { get; set; }
}

public enum UserPresenceStatus
{
    Online,
    Away,
    Busy,
    DoNotDisturb,
    Offline
}

public enum ChatMessageType
{
    Text,
    File,
    Image,
    Code,
    System,
    Notification
}
```

### 5.4 DTOs de Métricas e Monitoramento

#### IDE.Application/Realtime/DTOs/MetricsDTOs.cs
```csharp
public class CollaborationMetricsDto
{
    public string MetricName { get; set; }
    public MetricType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Dictionary<string, object>? Tags { get; set; }
}

public class CollaborationDashboardData
{
    // Colaboração
    public int ActiveUsers { get; set; }
    public double TotalEdits { get; set; }
    public double AverageLatency { get; set; }
    public double ConflictsResolved { get; set; }
    public double ChatMessages { get; set; }
    
    // Performance
    public int SignalRConnections { get; set; }
    public double RedisMemoryUsage { get; set; }
    public double OperationThroughput { get; set; }
    public double AverageConflictResolutionTime { get; set; }
    public double ReconnectionRate { get; set; }
    
    // Sistema
    public double RateLimitViolations { get; set; }
    public double AverageWorkspaceSize { get; set; }
    public UserDistributionDto UserDistribution { get; set; }
    public SystemHealthDto SystemHealth { get; set; }
    
    public DateTime GeneratedAt { get; set; }
}

public class UserDistributionDto
{
    public Dictionary<string, int> UsersByPlan { get; set; } = new();
    public Dictionary<string, int> UsersByRegion { get; set; } = new();
    public Dictionary<string, int> ActiveUsersByHour { get; set; } = new();
}

public class SystemHealthDto
{
    public HealthStatus Overall { get; set; }
    public HealthStatus Database { get; set; }
    public HealthStatus Redis { get; set; }
    public HealthStatus SignalR { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

public enum MetricType
{
    Counter,
    Gauge,
    Timer,
    Histogram
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}
```

### 5.5 DTOs de Rate Limiting e Auditoria

#### IDE.Application/Realtime/DTOs/SystemDTOs.cs
```csharp
public class RateLimitStatus
{
    public Guid UserId { get; set; }
    public int EditRequests { get; set; }
    public int ChatRequests { get; set; }
    public int CursorRequests { get; set; }
    public int PresenceRequests { get; set; }
    public DateTime LastReset { get; set; }
    public Dictionary<string, LimitInfo> Limits { get; set; } = new();
}

public class LimitInfo
{
    public int Current { get; set; }
    public int Maximum { get; set; }
    public TimeSpan Window { get; set; }
    public DateTime WindowStart { get; set; }
    public bool IsExceeded => Current >= Maximum;
}

public class CollaborationAuditLogDto
{
    public Guid Id { get; set; }
    public AuditAction Action { get; set; }
    public string Resource { get; set; }
    public string ResourceId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum AuditAction
{
    Create,
    Read,
    Update,
    Delete,
    Join,
    Leave,
    Connect,
    Disconnect,
    Send,
    Receive,
    Resolve,
    Reject
}
```

### 5.6 DTOs de Snapshot e Sincronização

#### IDE.Application/Realtime/DTOs/SnapshotDTOs.cs
```csharp
public class CollaborationSnapshotDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Content { get; set; }
    public long SequenceNumber { get; set; }
    public string ContentHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public List<TextOperationDto> PendingOperations { get; set; } = new();
    public Dictionary<string, object>? Metadata { get; set; }
}

public class SynchronizationRequestDto
{
    public Guid ItemId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public long LastKnownSequence { get; set; }
    public string? LastKnownHash { get; set; }
    public DateTime LastSyncTime { get; set; }
}

public class SynchronizationResponseDto
{
    public bool IsUpToDate { get; set; }
    public CollaborationSnapshotDto? Snapshot { get; set; }
    public List<TextOperationDto> MissingOperations { get; set; } = new();
    public List<ConflictDto> Conflicts { get; set; } = new();
    public long CurrentSequence { get; set; }
    public string CurrentHash { get; set; }
    public DateTime ServerTime { get; set; }
}

public class WorkspaceSyncStatus
{
    public Guid WorkspaceId { get; set; }
    public DateTime LastSync { get; set; }
    public bool IsOnline { get; set; }
    public int PendingOperations { get; set; }
    public int UnresolvedConflicts { get; set; }
    public List<UserPresenceDto> ActiveUsers { get; set; } = new();
    public Dictionary<string, object>? SyncMetadata { get; set; }
}
```

## 6. Controllers REST API para Colaboração

### 6.1 Controller de Colaboração Principal

#### IDE.API/Controllers/CollaborationController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CollaborationController : ControllerBase
{
    private readonly IOperationalTransformService _otService;
    private readonly ICollaborationAuditService _auditService;
    private readonly ICollaborationMetricsService _metricsService;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<CollaborationController> _logger;

    public CollaborationController(
        IOperationalTransformService otService,
        ICollaborationAuditService auditService,
        ICollaborationMetricsService metricsService,
        IRateLimitingService rateLimitingService,
        ILogger<CollaborationController> logger)
    {
        _otService = otService;
        _auditService = auditService;
        _metricsService = metricsService;
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    /// <summary>
    /// Processa uma operação de texto através do sistema OT
    /// </summary>
    [HttpPost("operations")]
    [ProducesResponseType(typeof(TransformOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TransformOperationResultDto>> ProcessOperation([FromBody] TextOperationDto operation)
    {
        var userId = GetCurrentUserId();
        var userPlan = await GetUserPlan(userId);

        // Rate limiting check
        if (!await _rateLimitingService.CheckEditLimitAsync(userId, userPlan))
        {
            return StatusCode(429, "Rate limit exceeded");
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _otService.ProcessOperationAsync(operation);
            stopwatch.Stop();

            // Métricas
            await _metricsService.RecordLatency("operation_processing", stopwatch.Elapsed, 
                new { workspaceId = operation.WorkspaceId.ToString(), userId = userId.ToString() });
            
            await _metricsService.IncrementAsync("edit_operations", 1, 
                new { workspaceId = operation.WorkspaceId.ToString() });

            // Auditoria
            await _auditService.LogAsync(AuditAction.Update, "item", operation.ItemId.ToString(), userId, 
                $"Operation: {operation.Type}, Position: {operation.Position}");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing operation for user {UserId}", userId);
            return BadRequest("Error processing operation");
        }
    }

    /// <summary>
    /// Processa um lote de operações
    /// </summary>
    [HttpPost("operations/batch")]
    [ProducesResponseType(typeof(List<TransformOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransformOperationResultDto>>> ProcessOperationBatch([FromBody] OperationBatchDto batch)
    {
        var userId = GetCurrentUserId();
        var userPlan = await GetUserPlan(userId);

        // Rate limiting para batch operations
        if (!await _rateLimitingService.CheckEditLimitAsync(userId, userPlan))
        {
            return StatusCode(429, "Rate limit exceeded");
        }

        var results = new List<TransformOperationResultDto>();

        foreach (var operation in batch.Operations)
        {
            try
            {
                var result = await _otService.ProcessOperationAsync(operation);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing operation in batch for user {UserId}", userId);
                results.Add(new TransformOperationResultDto 
                { 
                    TransformedOperation = operation,
                    RequiresManualResolution = true,
                    Conflicts = new List<ConflictDto> 
                    { 
                        new ConflictDto { Description = "Processing error", Type = ConflictType.SequenceViolation } 
                    }
                });
            }
        }

        await _metricsService.IncrementAsync("batch_operations", 1, 
            new { workspaceId = batch.WorkspaceId.ToString(), operationCount = batch.Operations.Count });

        return Ok(results);
    }

    /// <summary>
    /// Obtém snapshot atual de um item
    /// </summary>
    [HttpGet("workspaces/{workspaceId}/items/{itemId}/snapshot")]
    [ProducesResponseType(typeof(CollaborationSnapshotDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CollaborationSnapshotDto>> GetSnapshot(Guid workspaceId, Guid itemId)
    {
        var userId = GetCurrentUserId();
        
        try
        {
            var snapshot = await _otService.CreateSnapshotAsync(itemId);
            
            await _auditService.LogAsync(AuditAction.Read, "snapshot", itemId.ToString(), userId);
            
            return Ok(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting snapshot for item {ItemId}", itemId);
            return NotFound("Snapshot not found");
        }
    }

    /// <summary>
    /// Sincroniza estado de um item
    /// </summary>
    [HttpPost("synchronize")]
    [ProducesResponseType(typeof(SynchronizationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SynchronizationResponseDto>> Synchronize([FromBody] SynchronizationRequestDto request)
    {
        var userId = GetCurrentUserId();
        
        try
        {
            var response = await _otService.SynchronizeAsync(request);
            
            await _metricsService.IncrementAsync("synchronization_requests", 1, 
                new { workspaceId = request.WorkspaceId.ToString() });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing item {ItemId}", request.ItemId);
            return BadRequest("Synchronization failed");
        }
    }

    /// <summary>
    /// Resolve conflito manualmente
    /// </summary>
    [HttpPost("conflicts/{conflictId}/resolve")]
    [ProducesResponseType(typeof(ConflictResolutionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConflictResolutionDto>> ResolveConflict(Guid conflictId, [FromBody] ConflictResolutionDto resolution)
    {
        var userId = GetCurrentUserId();
        resolution.ResolvedByUserId = userId;
        resolution.ResolvedAt = DateTime.UtcNow;
        
        try
        {
            var result = await _otService.ResolveConflictAsync(conflictId, resolution);
            
            await _metricsService.IncrementAsync("conflicts_resolved", 1, 
                new { strategy = resolution.Strategy.ToString() });
            
            await _auditService.LogAsync(AuditAction.Resolve, "conflict", conflictId.ToString(), userId, 
                $"Strategy: {resolution.Strategy}");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict {ConflictId}", conflictId);
            return BadRequest("Error resolving conflict");
        }
    }

    private Guid GetCurrentUserId()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
    }

    private async Task<UserPlan> GetUserPlan(Guid userId)
    {
        // Implementation to get user plan
        return UserPlan.Free; // Default
    }
}
```

### 6.2 Controller de Presença

#### IDE.API/Controllers/PresenceController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PresenceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICollaborationMetricsService _metricsService;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<PresenceController> _logger;

    public PresenceController(
        ApplicationDbContext context,
        ICollaborationMetricsService metricsService,
        IRateLimitingService rateLimitingService,
        ILogger<PresenceController> logger)
    {
        _context = context;
        _metricsService = metricsService;
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém presença dos usuários em um workspace
    /// </summary>
    [HttpGet("workspaces/{workspaceId}")]
    [ProducesResponseType(typeof(List<UserPresenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserPresenceDto>>> GetWorkspacePresence(Guid workspaceId)
    {
        try
        {
            var presences = await _context.UserPresences
                .Where(p => p.WorkspaceId == workspaceId && p.Status != UserPresenceStatus.Offline)
                .Include(p => p.User)
                .ToListAsync();

            var dtos = presences.Select(p => new UserPresenceDto
            {
                UserId = p.UserId,
                UserName = p.User.UserName,
                DisplayName = p.User.DisplayName,
                Avatar = p.User.Avatar,
                Status = p.Status,
                WorkspaceId = p.WorkspaceId,
                CurrentItemId = p.CurrentItemId,
                LastSeen = p.LastSeen,
                Metadata = string.IsNullOrEmpty(p.Metadata) ? null : 
                    JsonSerializer.Deserialize<Dictionary<string, object>>(p.Metadata)
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workspace presence for {WorkspaceId}", workspaceId);
            return BadRequest("Error retrieving presence information");
        }
    }

    /// <summary>
    /// Atualiza status de presença do usuário
    /// </summary>
    [HttpPut("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> UpdatePresenceStatus([FromBody] UserPresenceDto presenceDto)
    {
        var userId = GetCurrentUserId();

        // Rate limiting check
        if (!await _rateLimitingService.CheckPresenceLimitAsync(userId))
        {
            return StatusCode(429, "Rate limit exceeded");
        }

        try
        {
            var presence = await _context.UserPresences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.WorkspaceId == presenceDto.WorkspaceId);

            if (presence == null)
            {
                presence = new UserPresence
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkspaceId = presenceDto.WorkspaceId
                };
                _context.UserPresences.Add(presence);
            }

            presence.Status = presenceDto.Status;
            presence.CurrentItemId = presenceDto.CurrentItemId;
            presence.LastSeen = DateTime.UtcNow;
            presence.Metadata = presenceDto.Metadata != null ? 
                JsonSerializer.Serialize(presenceDto.Metadata) : null;

            await _context.SaveChangesAsync();

            await _metricsService.IncrementAsync("presence_updates", 1, 
                new { workspaceId = presenceDto.WorkspaceId.ToString(), status = presenceDto.Status.ToString() });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating presence for user {UserId}", userId);
            return BadRequest("Error updating presence");
        }
    }

    /// <summary>
    /// Obtém cursores ativos em um item
    /// </summary>
    [HttpGet("workspaces/{workspaceId}/items/{itemId}/cursors")]
    [ProducesResponseType(typeof(List<UserCursorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserCursorDto>>> GetItemCursors(Guid workspaceId, Guid itemId)
    {
        try
        {
            var cursors = await _context.UserCursors
                .Where(c => c.ItemId == itemId && c.IsActive)
                .Include(c => c.User)
                .ToListAsync();

            var dtos = cursors.Select(c => new UserCursorDto
            {
                UserId = c.UserId,
                UserName = c.User.UserName,
                UserColor = c.UserColor,
                ItemId = c.ItemId,
                Position = c.Position,
                SelectionStart = c.SelectionStart,
                SelectionEnd = c.SelectionEnd,
                Timestamp = c.Timestamp,
                IsActive = c.IsActive
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cursors for item {ItemId}", itemId);
            return BadRequest("Error retrieving cursors");
        }
    }

    /// <summary>
    /// Atualiza posição do cursor
    /// </summary>
    [HttpPut("cursor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> UpdateCursor([FromBody] UserCursorDto cursorDto)
    {
        var userId = GetCurrentUserId();

        // Rate limiting check
        if (!await _rateLimitingService.CheckCursorLimitAsync(userId))
        {
            return StatusCode(429, "Rate limit exceeded");
        }

        try
        {
            var cursor = await _context.UserCursors
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ItemId == cursorDto.ItemId);

            if (cursor == null)
            {
                cursor = new UserCursor
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ItemId = cursorDto.ItemId,
                    UserColor = GenerateUserColor(userId)
                };
                _context.UserCursors.Add(cursor);
            }

            cursor.Position = cursorDto.Position;
            cursor.SelectionStart = cursorDto.SelectionStart;
            cursor.SelectionEnd = cursorDto.SelectionEnd;
            cursor.Timestamp = DateTime.UtcNow;
            cursor.IsActive = true;

            await _context.SaveChangesAsync();

            await _metricsService.IncrementAsync("cursor_updates", 1, 
                new { itemId = cursorDto.ItemId.ToString() });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cursor for user {UserId}", userId);
            return BadRequest("Error updating cursor");
        }
    }

    private Guid GetCurrentUserId()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
    }

    private string GenerateUserColor(Guid userId)
    {
        // Generate a consistent color based on user ID
        var colors = new[] { "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7", "#DDA0DD", "#98D8C8", "#F7DC6F" };
        return colors[Math.Abs(userId.GetHashCode()) % colors.Length];
    }
}
```

### 6.3 Controller de Métricas

#### IDE.API/Controllers/MetricsController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly ICollaborationMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        ICollaborationMetricsService metricsService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém dashboard de métricas de colaboração
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(CollaborationDashboardData), StatusCodes.Status200OK)]
    public async Task<ActionResult<CollaborationDashboardData>> GetDashboard([FromQuery] Guid? workspaceId = null)
    {
        try
        {
            var dashboard = await _metricsService.GetDashboardDataAsync(workspaceId);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            return BadRequest("Error retrieving dashboard data");
        }
    }

    /// <summary>
    /// Obtém métricas específicas por nome
    /// </summary>
    [HttpGet("{metricName}")]
    [ProducesResponseType(typeof(List<CollaborationMetricsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CollaborationMetricsDto>>> GetMetrics(
        string metricName, 
        [FromQuery] DateTime? from = null, 
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? workspaceId = null)
    {
        try
        {
            from ??= DateTime.UtcNow.AddDays(-1);
            to ??= DateTime.UtcNow;

            var metrics = await _metricsService.GetMetricsAsync(metricName, from.Value, to.Value, workspaceId);
            
            var dtos = metrics.Select(m => new CollaborationMetricsDto
            {
                MetricName = m.MetricName,
                Type = m.Type,
                Value = m.Value,
                Unit = m.Unit,
                Timestamp = m.Timestamp,
                WorkspaceId = m.WorkspaceId,
                Tags = string.IsNullOrEmpty(m.Tags) ? null : 
                    JsonSerializer.Deserialize<Dictionary<string, object>>(m.Tags)
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics for {MetricName}", metricName);
            return BadRequest("Error retrieving metrics");
        }
    }

    /// <summary>
    /// Registra métrica customizada
    /// </summary>
    [HttpPost("custom")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult> RecordCustomMetric([FromBody] CollaborationMetricsDto metricDto)
    {
        try
        {
            switch (metricDto.Type)
            {
                case MetricType.Counter:
                    await _metricsService.IncrementAsync(metricDto.MetricName, metricDto.Value, metricDto.Tags);
                    break;
                case MetricType.Gauge:
                    await _metricsService.SetGaugeAsync(metricDto.MetricName, metricDto.Value, metricDto.Tags);
                    break;
                case MetricType.Timer:
                    await _metricsService.RecordLatency(metricDto.MetricName, TimeSpan.FromMilliseconds(metricDto.Value), metricDto.Tags);
                    break;
            }

            return Created("", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording custom metric {MetricName}", metricDto.MetricName);
            return BadRequest("Error recording metric");
        }
    }
}
```

### 6.4 Controller de Chat

#### IDE.API/Controllers/ChatController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICollaborationMetricsService _metricsService;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ICollaborationAuditService _auditService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ApplicationDbContext context,
        ICollaborationMetricsService metricsService,
        IRateLimitingService rateLimitingService,
        ICollaborationAuditService auditService,
        ILogger<ChatController> logger)
    {
        _context = context;
        _metricsService = metricsService;
        _rateLimitingService = rateLimitingService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém histórico de mensagens do workspace
    /// </summary>
    [HttpGet("workspaces/{workspaceId}")]
    [ProducesResponseType(typeof(List<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(
        Guid workspaceId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? itemId = null)
    {
        try
        {
            var query = _context.ChatMessages
                .Where(m => m.WorkspaceId == workspaceId);

            if (itemId.HasValue)
            {
                query = query.Where(m => m.ItemId == itemId);
            }

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(m => m.User)
                .ToListAsync();

            var dtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                WorkspaceId = m.WorkspaceId,
                UserId = m.UserId,
                UserName = m.User.UserName,
                Content = m.Content,
                Type = m.Type,
                CreatedAt = m.CreatedAt,
                ReplyToId = m.ReplyToId,
                ItemId = m.ItemId,
                Metadata = string.IsNullOrEmpty(m.Metadata) ? null : 
                    JsonSerializer.Deserialize<Dictionary<string, object>>(m.Metadata)
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat messages for workspace {WorkspaceId}", workspaceId);
            return BadRequest("Error retrieving messages");
        }
    }

    /// <summary>
    /// Envia nova mensagem
    /// </summary>
    [HttpPost("workspaces/{workspaceId}/messages")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(Guid workspaceId, [FromBody] ChatMessageDto messageDto)
    {
        var userId = GetCurrentUserId();
        var userPlan = await GetUserPlan(userId);

        // Rate limiting check
        if (!await _rateLimitingService.CheckChatLimitAsync(userId, userPlan))
        {
            return StatusCode(429, "Rate limit exceeded");
        }

        try
        {
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Content = messageDto.Content,
                Type = messageDto.Type,
                CreatedAt = DateTime.UtcNow,
                ReplyToId = messageDto.ReplyToId,
                ItemId = messageDto.ItemId,
                Metadata = messageDto.Metadata != null ? 
                    JsonSerializer.Serialize(messageDto.Metadata) : null
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            await _metricsService.IncrementAsync("chat_messages", 1, 
                new { workspaceId = workspaceId.ToString(), type = messageDto.Type.ToString() });

            await _auditService.LogAsync(AuditAction.Send, "chat_message", message.Id.ToString(), userId, 
                $"Type: {messageDto.Type}");

            // Reload with user info
            message = await _context.ChatMessages
                .Include(m => m.User)
                .FirstAsync(m => m.Id == message.Id);

            var responseDto = new ChatMessageDto
            {
                Id = message.Id,
                WorkspaceId = message.WorkspaceId,
                UserId = message.UserId,
                UserName = message.User.UserName,
                Content = message.Content,
                Type = message.Type,
                CreatedAt = message.CreatedAt,
                ReplyToId = message.ReplyToId,
                ItemId = message.ItemId,
                Metadata = string.IsNullOrEmpty(message.Metadata) ? null : 
                    JsonSerializer.Deserialize<Dictionary<string, object>>(message.Metadata)
            };

            return Created("", responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message for user {UserId}", userId);
            return BadRequest("Error sending message");
        }
    }

    private Guid GetCurrentUserId()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());
    }

    private async Task<UserPlan> GetUserPlan(Guid userId)
    {
        // Implementation to get user plan
        return UserPlan.Free; // Default
    }
}
```

## Próximos Passos (Fase 4)

Na próxima fase, implementaremos:
- Performance e caching com Redis
- Sistema de logs estruturados
- Health checks e métricas
- Documentação Swagger completa
- Configurações de segurança para produção

**Dependências para Fase 4**: Esta fase deve estar 100% funcional antes de prosseguir.
