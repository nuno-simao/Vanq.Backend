# Fase 3 (Parte 3) – Infraestrutura EF Core & Parâmetros de Sistema

> Esta parte cobre: mapeamentos Entity Framework Core das novas entidades e os parâmetros de sistema (chaves configuráveis) para a colaboração em tempo real.

## 1. Objetivo
Consolidar as entidades (Parte 2) em um contexto EF Core configurado com índices, relacionamentos e constraints; e definir parâmetros tunáveis para governança, performance, segurança e limites de colaboração.

## 2. DbContext – Adições
Trecho ilustrativo de como as novas entidades são registradas e configuradas dentro de `ApplicationDbContext`.

```csharp
public class ApplicationDbContext : DbContext
{
    // ... DbSets existentes (Fases 1 e 2) ...

    // Novos DbSets
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<UserPresence> UserPresences { get; set; }
    public DbSet<EditorChange> EditorChanges { get; set; }
    public DbSet<UserCursor> UserCursors { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<TextOperation> TextOperations { get; set; }
    public DbSet<ConflictResolution> ConflictResolutions { get; set; }
    public DbSet<CollaborationSnapshot> CollaborationSnapshots { get; set; }
    public DbSet<CollaborationMetrics> CollaborationMetrics { get; set; }
    public DbSet<CollaborationAuditLog> CollaborationAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // ... configurações anteriores ...

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => new { e.WorkspaceId, e.CreatedAt });
            entity.HasOne(e => e.Workspace).WithMany().HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.ParentMessage).WithMany(m => m.Replies).HasForeignKey(e => e.ParentMessageId);
        });

        modelBuilder.Entity<UserPresence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConnectionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CurrentItemId).HasMaxLength(50);
            entity.HasIndex(e => e.ConnectionId).IsUnique();
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId });
        });

        modelBuilder.Entity<EditorChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).HasMaxLength(10000);
            entity.HasIndex(e => new { e.ItemId, e.Timestamp });
        });

        modelBuilder.Entity<UserCursor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserColor).HasMaxLength(10);
            entity.HasIndex(e => new { e.ItemId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ActionData).HasMaxLength(2000);
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt });
        });

        modelBuilder.Entity<TextOperation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(10000);
            entity.HasIndex(e => new { e.ItemId, e.SequenceNumber });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
        });

        modelBuilder.Entity<ConflictResolution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalOperation).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.TransformedOperation).HasMaxLength(5000);
            entity.Property(e => e.ResolutionData).HasMaxLength(5000);
            entity.HasIndex(e => new { e.ItemId, e.DetectedAt });
        });

        modelBuilder.Entity<CollaborationSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => new { e.ItemId, e.CreatedAt });
            entity.HasIndex(e => e.ContentHash);
        });

        modelBuilder.Entity<CollaborationMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MetricName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Tags).HasMaxLength(1000);
            entity.HasIndex(e => new { e.MetricName, e.Timestamp });
            entity.HasIndex(e => new { e.WorkspaceId, e.Type });
        });

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
        });
    }
}
```

### 2.1 Observações de Indexação
| Entidade | Índice | Finalidade |
|----------|--------|-----------|
| ChatMessage | (WorkspaceId, CreatedAt) | Paginação cronológica de chat |
| UserPresence | ConnectionId (unique) | Lookup rápido para disconnect |
| TextOperation | (ItemId, SequenceNumber) | Reconstrução determinística de estado |
| CollaborationSnapshot | ContentHash | Deduplicação e cache |
| CollaborationAuditLog | (Resource, ResourceId) | Filtragem por recurso |

## 3. Parâmetros de Sistema – CollaborationParameters
Controlam janelas de snapshot, limites, segurança e tuning de SignalR.

```csharp
public static class CollaborationParameters
{
    // Snapshot / Versionamento
    public const string COLLABORATION_SNAPSHOT_EVERY_EDITS = "collaboration.snapshot.every_edits";
    public const string COLLABORATION_SNAPSHOT_EVERY_MINUTES = "collaboration.snapshot.every_minutes";
    public const string COLLABORATION_SNAPSHOT_RETENTION_DAYS = "collaboration.snapshot.retention_days";
    public const string COLLABORATION_MAX_SNAPSHOTS_PER_ITEM = "collaboration.max_snapshots_per_item";

    // Rate Limiting (Planos)
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

    // Rate Limiting (Operação Genérica)
    public const string COLLABORATION_RATE_LIMIT_EDIT_OPERATION = "collaboration.rate_limit.operation.edit_per_minute";
    public const string COLLABORATION_RATE_LIMIT_CHAT_OPERATION = "collaboration.rate_limit.operation.chat_per_minute";
    public const string COLLABORATION_RATE_LIMIT_CURSOR_OPERATION = "collaboration.rate_limit.operation.cursor_per_second";
    public const string COLLABORATION_RATE_LIMIT_PRESENCE_OPERATION = "collaboration.rate_limit.operation.presence_per_minute";

    // Segurança
    public const string COLLABORATION_ENCRYPTION_LEVEL = "collaboration.security.encryption_level";
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

    public static readonly Dictionary<string,string> DefaultValues = new()
    {
        { COLLABORATION_SNAPSHOT_EVERY_EDITS, "25" },
        { COLLABORATION_SNAPSHOT_EVERY_MINUTES, "10" },
        { COLLABORATION_SNAPSHOT_RETENTION_DAYS, "5" },
        { COLLABORATION_MAX_SNAPSHOTS_PER_ITEM, "25" },
        { COLLABORATION_RATE_LIMIT_FREE_EDITS, "100" },
        { COLLABORATION_RATE_LIMIT_FREE_CHAT, "50" },
        { COLLABORATION_RATE_LIMIT_FREE_CURSOR, "30" },
        { COLLABORATION_RATE_LIMIT_FREE_CONNECTIONS, "2" },
        { COLLABORATION_RATE_LIMIT_PRO_EDITS, "500" },
        { COLLABORATION_RATE_LIMIT_PRO_CHAT, "200" },
        { COLLABORATION_RATE_LIMIT_PRO_CURSOR, "100" },
        { COLLABORATION_RATE_LIMIT_PRO_CONNECTIONS, "10" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_EDITS, "2000" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_CHAT, "1000" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_CURSOR, "500" },
        { COLLABORATION_RATE_LIMIT_ENTERPRISE_CONNECTIONS, "25" },
        { COLLABORATION_RATE_LIMIT_EDIT_OPERATION, "300" },
        { COLLABORATION_RATE_LIMIT_CHAT_OPERATION, "150" },
        { COLLABORATION_RATE_LIMIT_CURSOR_OPERATION, "60" },
        { COLLABORATION_RATE_LIMIT_PRESENCE_OPERATION, "30" },
        { COLLABORATION_ENCRYPTION_LEVEL, "Medium" },
        { COLLABORATION_AUDIT_RETENTION_DAYS, "30" },
        { COLLABORATION_SESSION_TIMEOUT_MINUTES, "60" },
        { COLLABORATION_MAX_USERS_PER_WORKSPACE, "20" },
        { COLLABORATION_MAX_CONCURRENT_EDITS, "5" },
        { COLLABORATION_METRICS_RETENTION_DAYS, "7" },
        { COLLABORATION_CLEANUP_INTERVAL_MINUTES, "30" },
        { COLLABORATION_SIGNALR_MAX_MESSAGE_SIZE, "1048576" },
        { COLLABORATION_SIGNALR_KEEPALIVE_INTERVAL, "15" },
        { COLLABORATION_SIGNALR_CLIENT_TIMEOUT, "30" },
        { COLLABORATION_SIGNALR_RECONNECT_ATTEMPTS, "5" }
    };
}
```

## 4. Estratégia de Tuning e Gestão
| Categoria | Parâmetro | Uso Prático |
|-----------|-----------|-------------|
| Snapshots | EVERY_EDITS | Frequência por número de operações |
| Snapshots | EVERY_MINUTES | Fallback temporal para consistência |
| Limites | *_edits/chat/cursor_* | Proteção contra abuso e DoS lógico |
| Segurança | AUDIT_RETENTION_DAYS | Limpeza de logs para LGPD/compliance |
| Performance | MAX_CONCURRENT_EDITS | Backpressure em hotspots |
| SignalR | MAX_MESSAGE_SIZE | Evitar payloads grandes bloqueando broadcast |

## 5. Recomendações Futuras
- Introduzir parâmetros de “backoff” para reconexões agressivas.
- Adicionar métricas de saturação (ex: % limite atingido por intervalo) para tuning automático.
- Suporte a overrides por workspace (multi-tenant avançado).

## 6. Próxima Leitura
Siga para a **Parte 4 – DTOs e Requests de Comunicação**.

---
_Parte 3 concluída. Próximo: 03-04-dtos-requests.md_
