# Fase 3.2: Entidades de Colaboração

## Continuação das Entidades para Colaboração em Tempo Real

Esta parte implementa as entidades essenciais para **edição colaborativa**, **cursores múltiplos** e **notificações em tempo real**. Estas entidades trabalham em conjunto com as entidades básicas da Parte 3.1.

**Pré-requisitos**: Parte 3.1 (Entidades Básicas) implementada

## 1. Entidades de Edição Colaborativa

### 1.1 EditorChange - Tracking de Mudanças

#### IDE.Domain/Entities/Realtime/EditorChange.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Registra mudanças feitas em itens para colaboração
    /// </summary>
    public class EditorChange
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Tipo da mudança: "insert", "delete", "replace"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; }
        
        /// <summary>
        /// Linha inicial da mudança (1-indexed)
        /// </summary>
        public int StartLine { get; set; }
        
        /// <summary>
        /// Coluna inicial da mudança (0-indexed)
        /// </summary>
        public int StartColumn { get; set; }
        
        /// <summary>
        /// Linha final da mudança (1-indexed)
        /// </summary>
        public int EndLine { get; set; }
        
        /// <summary>
        /// Coluna final da mudança (0-indexed)
        /// </summary>
        public int EndColumn { get; set; }
        
        /// <summary>
        /// Conteúdo da mudança
        /// </summary>
        [MaxLength(10000)]
        public string? Content { get; set; }
        
        /// <summary>
        /// Timestamp da mudança
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Número de sequência para ordenação
        /// </summary>
        public long SequenceNumber { get; set; }
        
        /// <summary>
        /// Hash do conteúdo antes da mudança (para validação)
        /// </summary>
        [MaxLength(64)]
        public string? PreviousContentHash { get; set; }
        
        /// <summary>
        /// Hash do conteúdo após a mudança
        /// </summary>
        [MaxLength(64)]
        public string? NewContentHash { get; set; }
        
        // Relacionamentos
        public Guid ItemId { get; set; }
        public ModuleItem Item { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Para debugging e audit
        public string? ClientInfo { get; set; } // JSON com info do cliente
        public string? ConflictResolution { get; set; } // Como conflitos foram resolvidos
        
        // Status da mudança
        public EditorChangeStatus Status { get; set; } = EditorChangeStatus.Applied;
        public string? StatusReason { get; set; }
    }
}
```

### 1.2 UserCursor - Posições dos Cursores

#### IDE.Domain/Entities/Realtime/UserCursor.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Posição do cursor de um usuário em um item
    /// </summary>
    public class UserCursor
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Posição absoluta no documento (character offset)
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// Linha atual (1-indexed)
        /// </summary>
        public int Line { get; set; }
        
        /// <summary>
        /// Coluna atual (0-indexed)
        /// </summary>
        public int Column { get; set; }
        
        /// <summary>
        /// Início da seleção (se houver)
        /// </summary>
        public int? SelectionStart { get; set; }
        
        /// <summary>
        /// Fim da seleção (se houver)
        /// </summary>
        public int? SelectionEnd { get; set; }
        
        /// <summary>
        /// Cor do cursor para exibição
        /// </summary>
        [MaxLength(10)]
        public string UserColor { get; set; }
        
        /// <summary>
        /// Se o cursor está ativo (usuário está editando)
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Timestamp da última atualização
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Tipo de seleção atual
        /// </summary>
        public CursorSelectionType SelectionType { get; set; } = CursorSelectionType.None;
        
        // Relacionamentos
        public Guid ItemId { get; set; }
        public ModuleItem Item { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Metadados da sessão
        public string? SessionId { get; set; }
        public string? EditorMode { get; set; } // "insert", "overwrite", "visual", etc.
        
        // Para otimização
        public DateTime LastBroadcast { get; set; } // Última vez que foi enviado via SignalR
        public bool RequiresBroadcast { get; set; } = true;
    }
}
```

### 1.3 Notification - Sistema de Notificações

#### IDE.Domain/Entities/Realtime/Notification.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Notificações em tempo real para usuários
    /// </summary>
    public class Notification
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Título da notificação
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }
        
        /// <summary>
        /// Conteúdo/mensagem da notificação
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; }
        
        /// <summary>
        /// Tipo da notificação
        /// </summary>
        public NotificationType Type { get; set; }
        
        /// <summary>
        /// Prioridade da notificação
        /// </summary>
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        
        /// <summary>
        /// Se foi lida pelo usuário
        /// </summary>
        public bool IsRead { get; set; } = false;
        
        /// <summary>
        /// Timestamp de criação
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Timestamp de leitura
        /// </summary>
        public DateTime? ReadAt { get; set; }
        
        /// <summary>
        /// URL para ação (opcional)
        /// </summary>
        [MaxLength(500)]
        public string? ActionUrl { get; set; }
        
        /// <summary>
        /// Dados extras da ação (JSON)
        /// </summary>
        [MaxLength(2000)]
        public string? ActionData { get; set; }
        
        /// <summary>
        /// Ícone personalizado
        /// </summary>
        [MaxLength(100)]
        public string? Icon { get; set; }
        
        /// <summary>
        /// Cor da notificação
        /// </summary>
        [MaxLength(10)]
        public string? Color { get; set; }
        
        // Relacionamentos
        public Guid? WorkspaceId { get; set; }
        public Workspace? Workspace { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        /// <summary>
        /// Usuário que triggou a notificação
        /// </summary>
        public Guid? TriggeredById { get; set; }
        public User? TriggeredBy { get; set; }
        
        // Para notificações relacionadas a itens específicos
        public Guid? RelatedItemId { get; set; }
        public ModuleItem? RelatedItem { get; set; }
        
        // Controle de entrega
        public DateTime? ExpiresAt { get; set; }
        public bool IsDelivered { get; set; } = false;
        public DateTime? DeliveredAt { get; set; }
        public int DeliveryAttempts { get; set; } = 0;
        public string? DeliveryError { get; set; }
        
        // Agrupamento de notificações similares
        public string? GroupingKey { get; set; }
        public int GroupCount { get; set; } = 1;
    }
}
```

## 2. Enums para Colaboração

### 2.1 Status de Mudanças no Editor

#### IDE.Domain/Entities/Realtime/Enums/EditorChangeStatus.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Status de uma mudança no editor
    /// </summary>
    public enum EditorChangeStatus
    {
        /// <summary>
        /// Mudança aplicada com sucesso
        /// </summary>
        Applied = 0,
        
        /// <summary>
        /// Mudança pendente (aguardando sincronização)
        /// </summary>
        Pending = 1,
        
        /// <summary>
        /// Mudança rejeitada por conflito
        /// </summary>
        Rejected = 2,
        
        /// <summary>
        /// Mudança com conflito resolvido
        /// </summary>
        ConflictResolved = 3,
        
        /// <summary>
        /// Mudança revertida
        /// </summary>
        Reverted = 4,
        
        /// <summary>
        /// Mudança mesclada com outra
        /// </summary>
        Merged = 5
    }
}
```

### 2.2 Tipos de Seleção do Cursor

#### IDE.Domain/Entities/Realtime/Enums/CursorSelectionType.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Tipos de seleção do cursor
    /// </summary>
    public enum CursorSelectionType
    {
        /// <summary>
        /// Nenhuma seleção
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Seleção de texto normal
        /// </summary>
        Text = 1,
        
        /// <summary>
        /// Seleção de linha inteira
        /// </summary>
        Line = 2,
        
        /// <summary>
        /// Seleção de bloco/coluna
        /// </summary>
        Block = 3,
        
        /// <summary>
        /// Seleção de palavra
        /// </summary>
        Word = 4,
        
        /// <summary>
        /// Seleção de parágrafo
        /// </summary>
        Paragraph = 5
    }
}
```

### 2.3 Tipos de Notificação

#### IDE.Domain/Entities/Realtime/Enums/NotificationType.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Tipos de notificação em tempo real
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// Convite para workspace
        /// </summary>
        WorkspaceInvitation = 0,
        
        /// <summary>
        /// Item criado
        /// </summary>
        ItemCreated = 1,
        
        /// <summary>
        /// Item atualizado
        /// </summary>
        ItemUpdated = 2,
        
        /// <summary>
        /// Promoção de fase
        /// </summary>
        PhasePromotion = 3,
        
        /// <summary>
        /// Usuário entrou no workspace
        /// </summary>
        UserJoined = 4,
        
        /// <summary>
        /// Usuário saiu do workspace
        /// </summary>
        UserLeft = 5,
        
        /// <summary>
        /// Menção no chat
        /// </summary>
        ChatMention = 6,
        
        /// <summary>
        /// Mensagem do sistema
        /// </summary>
        System = 7,
        
        /// <summary>
        /// Conflito detectado
        /// </summary>
        ConflictDetected = 8,
        
        /// <summary>
        /// Conflito resolvido
        /// </summary>
        ConflictResolved = 9,
        
        /// <summary>
        /// Backup/snapshot criado
        /// </summary>
        BackupCreated = 10,
        
        /// <summary>
        /// Erro de sincronização
        /// </summary>
        SyncError = 11,
        
        /// <summary>
        /// Rate limit atingido
        /// </summary>
        RateLimitExceeded = 12
    }
}
```

### 2.4 Prioridades de Notificação

#### IDE.Domain/Entities/Realtime/Enums/NotificationPriority.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Níveis de prioridade das notificações
    /// </summary>
    public enum NotificationPriority
    {
        /// <summary>
        /// Prioridade baixa (informativa)
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Prioridade normal
        /// </summary>
        Normal = 1,
        
        /// <summary>
        /// Prioridade alta (requer atenção)
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Crítica (requer ação imediata)
        /// </summary>
        Critical = 3
    }
}
```

## 3. Configuração do DbContext (Continuação)

### 3.1 Mapeamentos Adicionais

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Continuação)
```csharp
// Adicionar ao método OnModelCreating da classe ApplicationDbContext

// Configurações de EditorChange
modelBuilder.Entity<EditorChange>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Type)
        .IsRequired()
        .HasMaxLength(20);
    
    entity.Property(e => e.Content)
        .HasMaxLength(10000);
    
    entity.Property(e => e.PreviousContentHash)
        .HasMaxLength(64);
    
    entity.Property(e => e.NewContentHash)
        .HasMaxLength(64);
    
    entity.Property(e => e.Status)
        .HasConversion<int>();
    
    entity.Property(e => e.StatusReason)
        .HasMaxLength(500);
    
    // Índices para performance
    entity.HasIndex(e => new { e.ItemId, e.Timestamp })
        .HasDatabaseName("IX_EditorChange_Item_Timestamp");
    
    entity.HasIndex(e => new { e.UserId, e.Timestamp })
        .HasDatabaseName("IX_EditorChange_User_Timestamp");
    
    entity.HasIndex(e => e.SequenceNumber)
        .HasDatabaseName("IX_EditorChange_Sequence");
    
    // Relacionamentos
    entity.HasOne(e => e.Item)
        .WithMany()
        .HasForeignKey(e => e.ItemId)
        .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.User)
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Restrict);
});

// Configurações de UserCursor
modelBuilder.Entity<UserCursor>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.UserColor)
        .HasMaxLength(10);
    
    entity.Property(e => e.SessionId)
        .HasMaxLength(100);
    
    entity.Property(e => e.EditorMode)
        .HasMaxLength(20);
    
    entity.Property(e => e.SelectionType)
        .HasConversion<int>();
    
    // Índice único: um cursor por usuário por item
    entity.HasIndex(e => new { e.ItemId, e.UserId })
        .IsUnique()
        .HasDatabaseName("IX_UserCursor_Item_User_Unique");
    
    entity.HasIndex(e => new { e.IsActive, e.Timestamp })
        .HasDatabaseName("IX_UserCursor_Active_Timestamp");
    
    // Relacionamentos
    entity.HasOne(e => e.Item)
        .WithMany()
        .HasForeignKey(e => e.ItemId)
        .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.User)
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});

// Configurações de Notification
modelBuilder.Entity<Notification>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Title)
        .IsRequired()
        .HasMaxLength(200);
    
    entity.Property(e => e.Message)
        .IsRequired()
        .HasMaxLength(1000);
    
    entity.Property(e => e.Type)
        .HasConversion<int>();
    
    entity.Property(e => e.Priority)
        .HasConversion<int>();
    
    entity.Property(e => e.ActionUrl)
        .HasMaxLength(500);
    
    entity.Property(e => e.ActionData)
        .HasMaxLength(2000);
    
    entity.Property(e => e.Icon)
        .HasMaxLength(100);
    
    entity.Property(e => e.Color)
        .HasMaxLength(10);
    
    entity.Property(e => e.GroupingKey)
        .HasMaxLength(100);
    
    entity.Property(e => e.DeliveryError)
        .HasMaxLength(1000);
    
    // Índices para queries comuns
    entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt })
        .HasDatabaseName("IX_Notification_User_Read_Created");
    
    entity.HasIndex(e => new { e.WorkspaceId, e.Type, e.CreatedAt })
        .HasDatabaseName("IX_Notification_Workspace_Type_Created");
    
    entity.HasIndex(e => e.GroupingKey)
        .HasDatabaseName("IX_Notification_GroupingKey");
    
    entity.HasIndex(e => e.ExpiresAt)
        .HasDatabaseName("IX_Notification_ExpiresAt");
    
    // Relacionamentos
    entity.HasOne(e => e.Workspace)
        .WithMany()
        .HasForeignKey(e => e.WorkspaceId)
        .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.User)
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.TriggeredBy)
        .WithMany()
        .HasForeignKey(e => e.TriggeredById)
        .OnDelete(DeleteBehavior.SetNull);
    
    entity.HasOne(e => e.RelatedItem)
        .WithMany()
        .HasForeignKey(e => e.RelatedItemId)
        .OnDelete(DeleteBehavior.SetNull);
});
```

## 4. DTOs para as Novas Entidades

### 4.1 DTO para EditorChange

#### IDE.Application/Realtime/DTOs/EditorChangeDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class EditorChangeDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string? Content { get; set; }
        public DateTime Timestamp { get; set; }
        public long SequenceNumber { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserColor { get; set; }
        public EditorChangeStatus Status { get; set; }
        public string? StatusReason { get; set; }
    }
}
```

### 4.2 DTO para UserCursor

#### IDE.Application/Realtime/DTOs/UserCursorDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;

namespace IDE.Application.Realtime.DTOs
{
    public class UserCursorDto
    {
        public int Position { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int? SelectionStart { get; set; }
        public int? SelectionEnd { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserColor { get; set; }
        public bool IsActive { get; set; }
        public DateTime Timestamp { get; set; }
        public CursorSelectionType SelectionType { get; set; }
        public string? EditorMode { get; set; }
    }
}
```

### 4.3 DTO para Notification

#### IDE.Application/Realtime/DTOs/NotificationDto.cs
```csharp
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.DTOs.Users;

namespace IDE.Application.Realtime.DTOs
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionData { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public UserDto? TriggeredBy { get; set; }
        public Guid? RelatedItemId { get; set; }
        public string? RelatedItemName { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? GroupingKey { get; set; }
        public int GroupCount { get; set; }
        
        // Para UI
        public string PriorityText => GetPriorityText();
        public string PriorityColor => GetPriorityColor();
        public string TypeIcon => GetTypeIcon();
        
        private string GetPriorityText()
        {
            return Priority switch
            {
                NotificationPriority.Low => "Baixa",
                NotificationPriority.Normal => "Normal",
                NotificationPriority.High => "Alta",
                NotificationPriority.Critical => "Crítica",
                _ => "Normal"
            };
        }
        
        private string GetPriorityColor()
        {
            return Priority switch
            {
                NotificationPriority.Low => "#4CAF50",
                NotificationPriority.Normal => "#2196F3",
                NotificationPriority.High => "#FF9800",
                NotificationPriority.Critical => "#F44336",
                _ => "#2196F3"
            };
        }
        
        private string GetTypeIcon()
        {
            return Type switch
            {
                NotificationType.WorkspaceInvitation => "mail",
                NotificationType.ItemCreated => "add_circle",
                NotificationType.ItemUpdated => "edit",
                NotificationType.PhasePromotion => "upgrade",
                NotificationType.UserJoined => "person_add",
                NotificationType.UserLeft => "person_remove",
                NotificationType.ChatMention => "alternate_email",
                NotificationType.System => "settings",
                NotificationType.ConflictDetected => "warning",
                NotificationType.ConflictResolved => "check_circle",
                NotificationType.BackupCreated => "backup",
                NotificationType.SyncError => "sync_problem",
                NotificationType.RateLimitExceeded => "speed",
                _ => "notifications"
            };
        }
    }
}
```

## Entregáveis da Parte 3.2

✅ **EditorChange**: Tracking completo de mudanças colaborativas  
✅ **UserCursor**: Posicionamento de cursores múltiplos  
✅ **Notification**: Sistema de notificações em tempo real  
✅ **Enums avançados**: Status, tipos e prioridades  
✅ **Configuração DbContext**: Mapeamento das entidades colaborativas  
✅ **DTOs**: Estruturas para comunicação em tempo real  

## Próximos Passos

Na **Parte 3.3**, implementaremos:
- Entidades para Operational Transform
- System Parameters para colaboração
- Métricas e auditoria
- CollaborationSnapshot para versionamento

**Dependência**: Esta parte (3.2) deve estar implementada e testada antes de prosseguir.