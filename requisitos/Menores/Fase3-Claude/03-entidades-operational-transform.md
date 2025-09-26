# Fase 3.3: Entidades Operational Transform

## Entidades Avançadas para Colaboração

Esta parte implementa as entidades mais sofisticadas para **Operational Transform**, **métricas de colaboração** e **auditoria**. Estas entidades permitem resolver conflitos em tempo real e monitorar o sistema colaborativo.

**Pré-requisitos**: Partes 3.1 e 3.2 implementadas

## 1. Entidades para Operational Transform

### 1.1 TextOperation - Operações de Texto

#### IDE.Domain/Entities/Realtime/TextOperation.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Operação de texto para Operational Transform (algoritmo Google Wave)
    /// </summary>
    public class TextOperation
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Tipo da operação: Insert, Delete, Retain
        /// </summary>
        public OperationType Type { get; set; }
        
        /// <summary>
        /// Posição no documento (character offset)
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// Tamanho da operação (para Delete/Retain)
        /// </summary>
        public int Length { get; set; }
        
        /// <summary>
        /// Conteúdo da operação (para Insert)
        /// </summary>
        public string? Content { get; set; }
        
        /// <summary>
        /// Timestamp da operação
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// ID único do cliente que originou a operação
        /// </summary>
        public int ClientId { get; set; }
        
        /// <summary>
        /// Número de sequência para ordenação
        /// </summary>
        public int SequenceNumber { get; set; }
        
        /// <summary>
        /// Hash do estado do documento antes da operação
        /// </summary>
        [MaxLength(64)]
        public string? StateHashBefore { get; set; }
        
        /// <summary>
        /// Hash do estado do documento após a operação
        /// </summary>
        [MaxLength(64)]
        public string? StateHashAfter { get; set; }
        
        // Relacionamentos
        public Guid ItemId { get; set; }
        public ModuleItem Item { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Para tracking e debugging
        public string? OriginalOperation { get; set; } // JSON da operação original
        public string? TransformedOperation { get; set; } // JSON da operação após transformação
        public bool WasTransformed { get; set; } = false;
        
        // Metadados da operação
        public string? OperationMetadata { get; set; } // JSON
        public OperationSource Source { get; set; } = OperationSource.User;
        public OperationStatus Status { get; set; } = OperationStatus.Pending;
    }
}
```

### 1.2 ConflictResolution - Resolução de Conflitos

#### IDE.Domain/Entities/Realtime/ConflictResolution.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Registro de resolução de conflitos colaborativos
    /// </summary>
    public class ConflictResolution
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Tipo do conflito detectado
        /// </summary>
        public ConflictType Type { get; set; }
        
        /// <summary>
        /// Estratégia usada para resolução
        /// </summary>
        public ResolutionStrategy Strategy { get; set; }
        
        /// <summary>
        /// Operação original que causou conflito (JSON)
        /// </summary>
        [Required]
        [MaxLength(5000)]
        public string OriginalOperation { get; set; }
        
        /// <summary>
        /// Operação transformada/resolvida (JSON)
        /// </summary>
        [MaxLength(5000)]
        public string? TransformedOperation { get; set; }
        
        /// <summary>
        /// Dados adicionais da resolução (JSON)
        /// </summary>
        [MaxLength(5000)]
        public string? ResolutionData { get; set; }
        
        /// <summary>
        /// Quando o conflito foi detectado
        /// </summary>
        public DateTime DetectedAt { get; set; }
        
        /// <summary>
        /// Quando o conflito foi resolvido
        /// </summary>
        public DateTime ResolvedAt { get; set; }
        
        /// <summary>
        /// Tempo gasto na resolução
        /// </summary>
        public TimeSpan ResolutionTime => ResolvedAt - DetectedAt;
        
        /// <summary>
        /// Severidade do conflito
        /// </summary>
        public ConflictSeverity Severity { get; set; }
        
        /// <summary>
        /// Descrição legível do conflito
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        // Relacionamentos
        public Guid ItemId { get; set; }
        public ModuleItem Item { get; set; }
        
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        /// <summary>
        /// Usuário que resolveu o conflito (pode ser diferente de quem causou)
        /// </summary>
        public Guid? ResolvedByUserId { get; set; }
        public User? ResolvedBy { get; set; }
        
        // Operações envolvidas no conflito
        public List<TextOperation> ConflictingOperations { get; set; } = new();
        
        // Métricas
        public int OperationsAffected { get; set; }
        public int UsersAffected { get; set; }
        public bool RequiredUserIntervention { get; set; }
        public double AutomationConfidence { get; set; } // 0.0 a 1.0
    }
}
```

### 1.3 CollaborationSnapshot - Versionamento Híbrido

#### IDE.Domain/Entities/Realtime/CollaborationSnapshot.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Snapshot do conteúdo para otimização de sincronização
    /// </summary>
    public class CollaborationSnapshot
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Conteúdo completo no momento do snapshot
        /// </summary>
        [Required]
        public string Content { get; set; }
        
        /// <summary>
        /// Número de operações desde o último snapshot
        /// </summary>
        public int OperationCount { get; set; }
        
        /// <summary>
        /// Trigger que causou a criação do snapshot
        /// </summary>
        public SnapshotTrigger Trigger { get; set; }
        
        /// <summary>
        /// Timestamp de criação
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Tamanho do conteúdo em bytes
        /// </summary>
        public long ContentSize { get; set; }
        
        /// <summary>
        /// Hash do conteúdo para detecção de duplicatas
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string ContentHash { get; set; }
        
        /// <summary>
        /// Número de sequência da última operação incluída
        /// </summary>
        public int LastSequenceNumber { get; set; }
        
        /// <summary>
        /// Versão do algoritmo de snapshot usado
        /// </summary>
        [MaxLength(10)]
        public string SnapshotVersion { get; set; } = "1.0";
        
        // Relacionamentos
        public Guid ItemId { get; set; }
        public ModuleItem Item { get; set; }
        
        public Guid CreatedByUserId { get; set; }
        public User CreatedBy { get; set; }
        
        // Metadados do snapshot
        public string? SnapshotMetadata { get; set; } // JSON
        public CompressionType CompressionType { get; set; } = CompressionType.None;
        public double CompressionRatio { get; set; } = 1.0;
        
        // Para limpeza automática
        public DateTime? ExpiresAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        
        // Estatísticas do snapshot
        public int UserCount { get; set; } // Quantos usuários estavam ativos
        public TimeSpan CreationDuration { get; set; } // Tempo gasto criando
        public SnapshotQuality Quality { get; set; } = SnapshotQuality.Good;
    }
}
```

## 2. Entidades para Métricas e Monitoramento

### 2.1 CollaborationMetrics - Métricas do Sistema

#### IDE.Domain/Entities/Realtime/CollaborationMetrics.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Métricas de performance e uso da colaboração
    /// </summary>
    public class CollaborationMetrics
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Tipo da métrica
        /// </summary>
        public MetricType Type { get; set; }
        
        /// <summary>
        /// Nome da métrica
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string MetricName { get; set; }
        
        /// <summary>
        /// Valor da métrica
        /// </summary>
        public double Value { get; set; }
        
        /// <summary>
        /// Unidade de medida
        /// </summary>
        [MaxLength(20)]
        public string? Unit { get; set; }
        
        /// <summary>
        /// Tags/labels adicionais (JSON)
        /// </summary>
        [MaxLength(1000)]
        public string? Tags { get; set; }
        
        /// <summary>
        /// Timestamp da métrica
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        // Relacionamentos opcionais
        public Guid? WorkspaceId { get; set; }
        public Workspace? Workspace { get; set; }
        
        public Guid? UserId { get; set; }
        public User? User { get; set; }
        
        // Para agregações
        public TimeSpan? WindowSize { get; set; }
        public int? SampleCount { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? StandardDeviation { get; set; }
        
        // Metadados
        public string? Source { get; set; } // origem da métrica
        public string? Environment { get; set; } // dev, prod, etc
        public string? Version { get; set; } // versão do sistema
    }
}
```

### 2.2 CollaborationAuditLog - Log de Auditoria

#### IDE.Domain/Entities/Realtime/CollaborationAuditLog.cs
```csharp
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Realtime
{
    /// <summary>
    /// Log de auditoria para ações colaborativas
    /// </summary>
    public class CollaborationAuditLog
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Ação realizada
        /// </summary>
        public AuditAction Action { get; set; }
        
        /// <summary>
        /// Tipo de recurso afetado
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Resource { get; set; }
        
        /// <summary>
        /// ID do recurso afetado
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ResourceId { get; set; }
        
        /// <summary>
        /// Detalhes da ação (JSON)
        /// </summary>
        [MaxLength(2000)]
        public string? Details { get; set; }
        
        /// <summary>
        /// Endereço IP do usuário
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }
        
        /// <summary>
        /// User Agent do browser/cliente
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }
        
        /// <summary>
        /// Timestamp da ação
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Resultado da ação
        /// </summary>
        public AuditResult Result { get; set; } = AuditResult.Success;
        
        /// <summary>
        /// Código de erro (se houver)
        /// </summary>
        [MaxLength(50)]
        public string? ErrorCode { get; set; }
        
        /// <summary>
        /// Mensagem de erro (se houver)
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }
        
        // Relacionamentos
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        public Guid? WorkspaceId { get; set; }
        public Workspace? Workspace { get; set; }
        
        // Para correlação de eventos
        public Guid? CorrelationId { get; set; }
        public Guid? SessionId { get; set; }
        public string? TraceId { get; set; }
        
        // Dados de performance
        public TimeSpan? Duration { get; set; }
        public long? MemoryUsed { get; set; }
        public int? CpuUsage { get; set; }
        
        // Para retenção e limpeza
        public AuditSeverity Severity { get; set; } = AuditSeverity.Normal;
        public bool ShouldRetain { get; set; } = false;
        public DateTime? RetentionExpiresAt { get; set; }
    }
}
```

## 3. Enums para Operational Transform

### 3.1 Tipos de Operação

#### IDE.Domain/Entities/Realtime/Enums/OperationType.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Tipos de operação no Operational Transform
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Inserir texto na posição especificada
        /// </summary>
        Insert = 0,
        
        /// <summary>
        /// Deletar texto da posição especificada
        /// </summary>
        Delete = 1,
        
        /// <summary>
        /// Reter caracteres (não modificar)
        /// </summary>
        Retain = 2
    }
}
```

### 3.2 Tipos e Estratégias de Conflito

#### IDE.Domain/Entities/Realtime/Enums/ConflictEnums.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Tipos de conflito em colaboração
    /// </summary>
    public enum ConflictType
    {
        /// <summary>
        /// Conflito simples (inserções/deleções não sobrepostas)
        /// </summary>
        Simple = 0,
        
        /// <summary>
        /// Conflito complexo (operações sobrepostas)
        /// </summary>
        Complex = 1,
        
        /// <summary>
        /// Conflito crítico (requer intervenção manual)
        /// </summary>
        Critical = 2
    }

    /// <summary>
    /// Estratégias para resolução de conflitos
    /// </summary>
    public enum ResolutionStrategy
    {
        /// <summary>
        /// Merge automático usando algoritmo
        /// </summary>
        AutomaticMerge = 0,
        
        /// <summary>
        /// Usuário escolhe manualmente
        /// </summary>
        UserChoice = 1,
        
        /// <summary>
        /// Primeira operação tem prioridade
        /// </summary>
        FirstWins = 2,
        
        /// <summary>
        /// Última operação tem prioridade
        /// </summary>
        LastWins = 3,
        
        /// <summary>
        /// Requer revisão manual
        /// </summary>
        ManualReview = 4
    }

    /// <summary>
    /// Severidade do conflito
    /// </summary>
    public enum ConflictSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
```

### 3.3 Enums de Snapshot e Sistema

#### IDE.Domain/Entities/Realtime/Enums/SystemEnums.cs
```csharp
namespace IDE.Domain.Entities.Realtime.Enums
{
    /// <summary>
    /// Triggers para criação de snapshot
    /// </summary>
    public enum SnapshotTrigger
    {
        /// <summary>
        /// A cada X operações
        /// </summary>
        OperationCount = 0,
        
        /// <summary>
        /// A cada X minutos
        /// </summary>
        TimeInterval = 1,
        
        /// <summary>
        /// Solicitação manual do usuário
        /// </summary>
        Manual = 2,
        
        /// <summary>
        /// Após resolução de conflito
        /// </summary>
        Conflict = 3,
        
        /// <summary>
        /// Ao desconectar usuário
        /// </summary>
        Shutdown = 4
    }

    /// <summary>
    /// Tipos de métrica
    /// </summary>
    public enum MetricType
    {
        /// <summary>
        /// Contador (valores incrementais)
        /// </summary>
        Counter = 0,
        
        /// <summary>
        /// Gauge (valores instantâneos)
        /// </summary>
        Gauge = 1,
        
        /// <summary>
        /// Histograma (distribuição de valores)
        /// </summary>
        Histogram = 2,
        
        /// <summary>
        /// Timer (medição de tempo)
        /// </summary>
        Timer = 3
    }

    /// <summary>
    /// Ações de auditoria
    /// </summary>
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

    /// <summary>
    /// Origem da operação
    /// </summary>
    public enum OperationSource
    {
        User = 0,
        System = 1,
        AutoMerge = 2,
        ConflictResolution = 3
    }

    /// <summary>
    /// Status da operação
    /// </summary>
    public enum OperationStatus
    {
        Pending = 0,
        Applied = 1,
        Rejected = 2,
        Transformed = 3
    }

    /// <summary>
    /// Tipo de compressão do snapshot
    /// </summary>
    public enum CompressionType
    {
        None = 0,
        Gzip = 1,
        Brotli = 2
    }

    /// <summary>
    /// Qualidade do snapshot
    /// </summary>
    public enum SnapshotQuality
    {
        Poor = 0,
        Fair = 1,
        Good = 2,
        Excellent = 3
    }

    /// <summary>
    /// Resultado da auditoria
    /// </summary>
    public enum AuditResult
    {
        Success = 0,
        Failure = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Severidade da auditoria
    /// </summary>
    public enum AuditSeverity
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
}
```

## 4. System Parameters para Colaboração

### 4.1 Parâmetros de Configuração

#### IDE.Domain/Entities/SystemParameter.cs (Extensão - Collaboration Constants)
```csharp
using IDE.Domain.Entities;

namespace IDE.Domain.Entities
{
    // Extensão da classe SystemParameter existente para incluir constantes de colaboração
    public partial class SystemParameter
    {
        // Constantes específicas para colaboração - complementam as constantes existentes
        
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
        
        public const string COLLABORATION_RATE_LIMIT_PREMIUM_EDITS = "collaboration.rate_limit.premium.edits_per_minute";
        public const string COLLABORATION_RATE_LIMIT_PREMIUM_CHAT = "collaboration.rate_limit.premium.chat_per_minute";
        public const string COLLABORATION_RATE_LIMIT_PREMIUM_CURSOR = "collaboration.rate_limit.premium.cursor_per_second";
        public const string COLLABORATION_RATE_LIMIT_PREMIUM_CONNECTIONS = "collaboration.rate_limit.premium.max_connections";
        
        // Performance
        public const string COLLABORATION_MAX_USERS_PER_WORKSPACE = "collaboration.performance.max_users_per_workspace";
        public const string COLLABORATION_MAX_CONCURRENT_EDITS = "collaboration.performance.max_concurrent_edits";
        public const string COLLABORATION_METRICS_RETENTION_DAYS = "collaboration.performance.metrics_retention_days";
        public const string COLLABORATION_CLEANUP_INTERVAL_MINUTES = "collaboration.performance.cleanup_interval_minutes";
        
        // SignalR
        public const string COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE = "collaboration.signalr.max_message_size";
        public const string COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL = "collaboration.signalr.keepalive_interval_seconds";
        public const string COLLABORATION_SIGNALR_CLIENT_TIMEOUT = "collaboration.signalr.client_timeout_seconds";
        
        /// <summary>
        /// Valores padrão para parâmetros de colaboração - complementam os existentes
        /// </summary>
        public static Dictionary<string, string> GetCollaborationDefaults()
        {
            return new Dictionary<string, string>
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
                
                // Rate Limiting Premium  
                { COLLABORATION_RATE_LIMIT_PREMIUM_EDITS, "500" },
                { COLLABORATION_RATE_LIMIT_PREMIUM_CHAT, "200" },
                { COLLABORATION_RATE_LIMIT_PREMIUM_CURSOR, "50" },
                { COLLABORATION_RATE_LIMIT_PREMIUM_CONNECTIONS, "5" },
                
                // Performance
                { COLLABORATION_MAX_USERS_PER_WORKSPACE, "20" },
                { COLLABORATION_MAX_CONCURRENT_EDITS, "5" },
                { COLLABORATION_METRICS_RETENTION_DAYS, "7" },
                { COLLABORATION_CLEANUP_INTERVAL_MINUTES, "30" },
                
                // SignalR
                { COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE, "1048576" }, // 1MB
                { COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL, "15" },
                { COLLABORATION_SIGNALR_CLIENT_TIMEOUT, "30" }
            };
        }
    }
}
```csharp
// Adicionar estes parâmetros à classe SystemParameter existente

namespace IDE.Domain.Entities
{
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
        
        // Performance
        public const string COLLABORATION_MAX_USERS_PER_WORKSPACE = "collaboration.performance.max_users_per_workspace";
        public const string COLLABORATION_MAX_CONCURRENT_EDITS = "collaboration.performance.max_concurrent_edits";
        public const string COLLABORATION_METRICS_RETENTION_DAYS = "collaboration.performance.metrics_retention_days";
        public const string COLLABORATION_CLEANUP_INTERVAL_MINUTES = "collaboration.performance.cleanup_interval_minutes";
        
        // SignalR
        public const string COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE = "collaboration.signalr.max_message_size";
        public const string COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL = "collaboration.signalr.keepalive_interval_seconds";
        public const string COLLABORATION_SIGNALR_CLIENT_TIMEOUT = "collaboration.signalr.client_timeout_seconds";
        
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
            
            // Performance
            { COLLABORATION_MAX_USERS_PER_WORKSPACE, "20" },
            { COLLABORATION_MAX_CONCURRENT_EDITS, "5" },
            { COLLABORATION_METRICS_RETENTION_DAYS, "7" },
            { COLLABORATION_CLEANUP_INTERVAL_MINUTES, "30" },
            
            // SignalR
            { COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE, "1048576" }, // 1MB
            { COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL, "15" },
            { COLLABORATION_SIGNALR_CLIENT_TIMEOUT, "30" }
        };
    }
}
```

## 5. Configuração Final do DbContext

### 5.1 Mapeamentos das Entidades OT

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Final)
```csharp
// Adicionar ao método OnModelCreating

// Configurações de TextOperation
modelBuilder.Entity<TextOperation>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Type)
        .HasConversion<int>();
    
    entity.Property(e => e.Content)
        .HasMaxLength(10000);
    
    entity.Property(e => e.StateHashBefore)
        .HasMaxLength(64);
    
    entity.Property(e => e.StateHashAfter)
        .HasMaxLength(64);
    
    entity.Property(e => e.Source)
        .HasConversion<int>();
    
    entity.Property(e => e.Status)
        .HasConversion<int>();
    
    // Índices para performance
    entity.HasIndex(e => new { e.ItemId, e.SequenceNumber })
        .HasDatabaseName("IX_TextOperation_Item_Sequence");
    
    entity.HasIndex(e => new { e.UserId, e.Timestamp })
        .HasDatabaseName("IX_TextOperation_User_Timestamp");
    
    entity.HasIndex(e => new { e.ClientId, e.Timestamp })
        .HasDatabaseName("IX_TextOperation_Client_Timestamp");
    
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

// Configurações de ConflictResolution
modelBuilder.Entity<ConflictResolution>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Type)
        .HasConversion<int>();
    
    entity.Property(e => e.Strategy)
        .HasConversion<int>();
    
    entity.Property(e => e.Severity)
        .HasConversion<int>();
    
    // Relacionamentos
    entity.HasOne(e => e.Item)
        .WithMany()
        .HasForeignKey(e => e.ItemId)
        .OnDelete(DeleteBehavior.Cascade);
    
    entity.HasOne(e => e.User)
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Restrict);
    
    entity.HasOne(e => e.ResolvedBy)
        .WithMany()
        .HasForeignKey(e => e.ResolvedByUserId)
        .OnDelete(DeleteBehavior.SetNull);
});

// Configurações de CollaborationSnapshot
modelBuilder.Entity<CollaborationSnapshot>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Trigger)
        .HasConversion<int>();
    
    entity.Property(e => e.CompressionType)
        .HasConversion<int>();
    
    entity.Property(e => e.Quality)
        .HasConversion<int>();
    
    // Índices
    entity.HasIndex(e => new { e.ItemId, e.CreatedAt })
        .HasDatabaseName("IX_CollaborationSnapshot_Item_Created");
    
    entity.HasIndex(e => e.ContentHash)
        .HasDatabaseName("IX_CollaborationSnapshot_Hash");
});

// Configurações de CollaborationMetrics
modelBuilder.Entity<CollaborationMetrics>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Type)
        .HasConversion<int>();
    
    // Índices
    entity.HasIndex(e => new { e.MetricName, e.Timestamp })
        .HasDatabaseName("IX_CollaborationMetrics_Name_Timestamp");
    
    entity.HasIndex(e => new { e.WorkspaceId, e.Type })
        .HasDatabaseName("IX_CollaborationMetrics_Workspace_Type");
});

// Configurações de CollaborationAuditLog
modelBuilder.Entity<CollaborationAuditLog>(entity =>
{
    entity.HasKey(e => e.Id);
    
    entity.Property(e => e.Action)
        .HasConversion<int>();
    
    entity.Property(e => e.Result)
        .HasConversion<int>();
    
    entity.Property(e => e.Severity)
        .HasConversion<int>();
    
    // Índices
    entity.HasIndex(e => new { e.UserId, e.Timestamp })
        .HasDatabaseName("IX_CollaborationAuditLog_User_Timestamp");
    
    entity.HasIndex(e => new { e.Resource, e.ResourceId })
        .HasDatabaseName("IX_CollaborationAuditLog_Resource");
});
```

## Entregáveis da Parte 3.3

✅ **TextOperation**: Sistema completo de Operational Transform  
✅ **ConflictResolution**: Resolução automática e manual de conflitos  
✅ **CollaborationSnapshot**: Versionamento híbrido otimizado  
✅ **CollaborationMetrics**: Monitoramento de performance  
✅ **CollaborationAuditLog**: Auditoria completa de ações  
✅ **Enums OT**: Tipos, estratégias e configurações  
✅ **System Parameters**: Configuração dinâmica do sistema  
✅ **DbContext completo**: Mapeamento de todas as entidades  

## Próximos Passos

Na **Parte 3.4**, implementaremos:
- SignalR Hub - Conexões e grupos
- Gestão de workspaces em tempo real
- Sharding e load balancing
- Rate limiting básico

**Dependência**: Esta parte (3.3) deve estar implementada e testada antes de prosseguir.