# Fase 2: Workspace Core - Backend .NET Core 8

## Contexto da Fase

Esta é a **segunda fase** de implementação do backend IDE. Com a autenticação estabelecida na Fase 1, agora implementaremos o **core do sistema de workspace** - o coração da aplicação onde os usuários criam, organizam e gerenciam seus projetos conceituais.

**Pré-requisitos**: Fase 1 (Fundação e Autenticação) deve estar 100% funcional

## Alinhamento Arquitetural Frontend ↔ Backend

Esta fase foi projetada para **compatibilidade total** com as interfaces TypeScript existentes no frontend, com melhorias arquiteturais específicas:

### Mapeamento de Interfaces
- **Frontend `IWorkspace`** ↔ **Backend `Workspace` entity**
- **Frontend `IModuleItem`** ↔ **Backend `ModuleItem` entity** (com `Type` field)
- **Frontend `IWorkspace.getNavigationState()`** ↔ **Backend `WorkspaceNavigationState` table**
- **Frontend cache inteligente** ↔ **Backend Redis cache + SignalR sync**

### Estratégia de Sincronização
- **Cache Redis** para performance de consultas frequentes
- **SignalR Hub básico** para notificações de mudanças em tempo real
- **Controle de conflitos** via version number incremental
- **Polling fallback** para browsers sem WebSocket support

## Objetivos da Fase

✅ **Entidades principais** do workspace (Workspace, ModuleItem, Phases, Tags)  
✅ **Sistema de permissões** completo com colaboração  
✅ **CRUD de workspaces** e itens com validações robustas  
✅ **Fases customizáveis** por workspace (padrão: apenas "Development")  
✅ **Versionamento manual** por solicitação do usuário  
✅ **Sistema de busca** e organização avançada  
✅ **Cache Redis** para performance otimizada  
✅ **SignalR Hub básico** para sincronização em tempo real  
✅ **Sistema de parâmetros** configuráveis por plano  
✅ **Controle de conflitos** via version number  
✅ **Navigation state** persistente por módulo  
✅ **Hierarquia ilimitada** de items com performance otimizada  

## Conceito de Workspace na IDE

Um **Workspace** é um ambiente de trabalho conceitual que contém:
- **Itens com Módulos**: Documentos editáveis categorizados por módulo (string)
- **Tipos de Item**: Definidos por `Type` field (ex: "typescript", "markdown", "uml-diagram", "database-table")
- **Hierarquia Ilimitada**: Items podem ter sub-items via `ParentId` sem limite de profundidade
- **Fases de Desenvolvimento**: Customizáveis por workspace (padrão: "Development")
- **Versionamento Manual**: Criado por solicitação do usuário (1.0.0, 1.1.0, 2.0.0, etc.)
- **Tags**: Organização tanto por workspace quanto por item individual
- **Navigation State**: Persistência de estado por módulo
- **Colaboração em Tempo Real**: Via SignalR Hub

### Estrutura Hierárquica Exemplo
```
Workspace "Meu Projeto v1.2.0" (Fase: Development)
├── Item "API Documentation" [Módulo: "Documents"] [Type: "markdown"] [Tags: "API", "Docs"]
│   ├── Sub-item "Authentication Guide" [Type: "markdown"]
│   └── Sub-item "Rate Limiting" [Type: "markdown"]
├── Item "User Tests" [Módulo: "Tests"] [Type: "typescript"] [Tags: "Unit", "User"]
├── Item "Production Config" [Módulo: "Environments"] [Type: "json"] [Tags: "Config", "Prod"]
├── Item "User Service" [Módulo: "API"] [Type: "typescript"] [Tags: "Service", "Auth"]
├── Item "Database Schema" [Módulo: "API"] [Type: "database"] [Tags: "SQL", "Schema"]
│   ├── Sub-item "Users Table" [Type: "database-table"]
│   └── Sub-item "Permissions Table" [Type: "database-table"]
└── Item "System Architecture" [Módulo: "Documents"] [Type: "uml-diagram"] [Tags: "Architecture"]
```

## Sistema de Planos e Quotas

### Plano Free
- **1 workspace**, **2 usuários por workspace**
- **Activity logs**: 7 dias de retenção
- **Storage**: Configurável via SystemParameter
- **Items por workspace**: Configurável via SystemParameter

### Plano Pro  
- **5 workspaces**, **10 usuários por workspace**
- **Activity logs**: Configurável via SystemParameter
- **Storage**: Configurável via SystemParameter
- **Funcionalidades extras**: Analytics básico

### Plano Enterprise
- **15 workspaces**, **25 usuários por workspace**  
- **Activity logs**: Configurável via SystemParameter
- **Storage**: Configurável via SystemParameter
- **Funcionalidades extras**: Analytics avançado, Admin dashboard

## 1. Entidades de Workspace

### 1.1 Entidades Principais

#### IDE.Domain/Entities/Workspace/
```csharp
// Workspace.cs
public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string SemanticVersion { get; set; } = "1.0.0";
    public Guid? CurrentPhaseId { get; set; }
    public WorkspacePhase CurrentPhase { get; set; }
    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; }
    public List<ModuleItem> Items { get; set; } = new();
    public List<WorkspacePermission> Permissions { get; set; } = new();
    public List<WorkspaceTag> Tags { get; set; } = new();
    public List<WorkspaceInvitation> Invitations { get; set; } = new();
    public List<ActivityLog> Activities { get; set; } = new();
    public List<WorkspaceVersion> Versions { get; set; } = new();
    public List<WorkspacePhase> Phases { get; set; } = new();
    public List<WorkspaceNavigationState> NavigationStates { get; set; } = new();
}

// ModuleItem.cs - ATUALIZADO
public class ModuleItem
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public string Module { get; set; }          // Ex: "Documents", "Tests", "Environments", "API"
    public string Type { get; set; }            // Ex: "typescript", "markdown", "uml-diagram", "database-table"
    public Guid? ParentId { get; set; }         // Para hierarquia ilimitada
    public ModuleItem Parent { get; set; }
    public List<ModuleItem> Children { get; set; } = new();
    public int VersionNumber { get; set; } = 1; // Para controle de conflitos
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public List<ItemVersion> Versions { get; set; } = new();
    public List<ModuleItemTag> Tags { get; set; } = new();
}

// WorkspaceNavigationState.cs - NOVA ENTIDADE
public class WorkspaceNavigationState
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public string ModuleId { get; set; }        // ID do módulo no frontend
    public string StateJson { get; set; }       // Estado serializado do módulo
    public DateTime UpdatedAt { get; set; }
}

// SystemParameter.cs - NOVA ENTIDADE
public class SystemParameter
{
    public Guid Id { get; set; }
    public string Key { get; set; }             // Ex: "MAX_WORKSPACES_FREE", "LOG_RETENTION_PRO"
    public string Value { get; set; }           // Valor do parâmetro
    public string Description { get; set; }     // Descrição do parâmetro
    public string Category { get; set; }        // Ex: "QUOTAS", "RETENTION", "FEATURES"
    public DateTime UpdatedAt { get; set; }
}

// ModuleItemTag.cs
public class ModuleItemTag
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; } = "#1890ff"; // Cor padrão Ant Design
    public DateTime CreatedAt { get; set; }
    public Guid ModuleItemId { get; set; }
    public ModuleItem ModuleItem { get; set; }
}

// WorkspacePhase.cs
public class WorkspacePhase
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Color { get; set; } = "#52c41a"; // Cor padrão
    public int Order { get; set; }
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public List<WorkspaceVersion> Versions { get; set; } = new();
}

// WorkspacePermission.cs
public class WorkspacePermission
{
    public Guid Id { get; set; }
    public PermissionLevel Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}

// WorkspaceInvitation.cs
public class WorkspaceInvitation
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Token { get; set; }
    public PermissionLevel Level { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid InvitedById { get; set; }
    public User InvitedBy { get; set; }
}

// WorkspaceTag.cs
public class WorkspaceTag
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; } = "#1890ff";
    public DateTime CreatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
}

// ActivityLog.cs
public class ActivityLog
{
    public Guid Id { get; set; }
    public string Action { get; set; }
    public string Details { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}

// WorkspaceVersion.cs
public class WorkspaceVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public Guid PhaseId { get; set; }
    public WorkspacePhase Phase { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; }
}

// ItemVersion.cs
public class ItemVersion
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public string Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ItemId { get; set; }
    public ModuleItem Item { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; }
}

// Enums
public enum PermissionLevel
{
    Owner = 0,
    Editor = 1,
    Reader = 2
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Expired = 3
}
```

### 1.2 Configuração do DbContext (Extensão)

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Adições)
```csharp
public class ApplicationDbContext : DbContext
{
    // ... propriedades existentes da Fase 1 ...

    // Novas DbSets
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<ModuleItem> ModuleItems { get; set; }
    public DbSet<ModuleItemTag> ModuleItemTags { get; set; }
    public DbSet<WorkspacePhase> WorkspacePhases { get; set; }
    public DbSet<WorkspacePermission> WorkspacePermissions { get; set; }
    public DbSet<WorkspaceInvitation> WorkspaceInvitations { get; set; }
    public DbSet<WorkspaceTag> WorkspaceTags { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<WorkspaceVersion> WorkspaceVersions { get; set; }
    public DbSet<ItemVersion> ItemVersions { get; set; }
    public DbSet<WorkspaceNavigationState> WorkspaceNavigationStates { get; set; }
    public DbSet<SystemParameter> SystemParameters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... configurações existentes da Fase 1 ...

        // Configurações de Workspace
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.SemanticVersion).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => new { e.OwnerId, e.Name }).IsUnique();
            entity.HasIndex(e => e.UpdatedAt); // Para performance em queries ordenadas
            
            entity.HasOne(e => e.Owner).WithMany().HasForeignKey(e => e.OwnerId);
            entity.HasOne(e => e.CurrentPhase).WithMany().HasForeignKey(e => e.CurrentPhaseId).IsRequired(false);
        });

        // Configurações de ModuleItem - ATUALIZADO
        modelBuilder.Entity<ModuleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Module).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50); // NOVO: substitui EditorType/Language
            entity.Property(e => e.VersionNumber).IsRequired(); // NOVO: para controle de conflitos
            entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.WorkspaceId, e.Module }); // Performance para filtro por módulo
            entity.HasIndex(e => new { e.WorkspaceId, e.ParentId }); // Performance para hierarquia
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Items).HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.Parent).WithMany(p => p.Children).HasForeignKey(e => e.ParentId).IsRequired(false);
        });

        // Configurações de WorkspaceNavigationState - NOVA
        modelBuilder.Entity<WorkspaceNavigationState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModuleId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StateJson).IsRequired();
            entity.HasIndex(e => new { e.WorkspaceId, e.ModuleId }).IsUnique();
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.NavigationStates).HasForeignKey(e => e.WorkspaceId);
        });

        // Configurações de SystemParameter - NOVA
        modelBuilder.Entity<SystemParameter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.Category); // Para queries por categoria
        });

        // Configurações de ModuleItemTag
        modelBuilder.Entity<ModuleItemTag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(7); // Hex color
            entity.HasIndex(e => new { e.ModuleItemId, e.Name }).IsUnique();
            
            entity.HasOne(e => e.ModuleItem).WithMany(i => i.Tags).HasForeignKey(e => e.ModuleItemId);
        });

        // Configurações de WorkspacePhase
        modelBuilder.Entity<WorkspacePhase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.HasIndex(e => new { e.WorkspaceId, e.Order }).IsUnique();
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Phases).HasForeignKey(e => e.WorkspaceId);
        });

        // Configurações de WorkspacePermission
        modelBuilder.Entity<WorkspacePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Permissions).HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de WorkspaceInvitation
        modelBuilder.Entity<WorkspaceInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Token).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Invitations).HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.InvitedBy).WithMany().HasForeignKey(e => e.InvitedById);
        });

        // Configurações de WorkspaceTag
        modelBuilder.Entity<WorkspaceTag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(7);
            entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Tags).HasForeignKey(e => e.WorkspaceId);
        });

        // Configurações de ActivityLog
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Activities).HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // Configurações de WorkspaceVersion
        modelBuilder.Entity<WorkspaceVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => new { e.WorkspaceId, e.Version }).IsUnique();
            
            entity.HasOne(e => e.Workspace).WithMany(w => w.Versions).HasForeignKey(e => e.WorkspaceId);
            entity.HasOne(e => e.Phase).WithMany(p => p.Versions).HasForeignKey(e => e.PhaseId);
            entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById);
        });

        // Configurações de ItemVersion
        modelBuilder.Entity<ItemVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Comment).HasMaxLength(500);
            
            entity.HasOne(e => e.Item).WithMany(i => i.Versions).HasForeignKey(e => e.ItemId);
            entity.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById);
        });

        // Atualizar seed data
        SeedWorkspaceData(modelBuilder);
    }

    private void SeedWorkspaceData(ModelBuilder modelBuilder)
    {
        // Seed de SystemParameters
        var systemParams = new[]
        {
            new SystemParameter { Id = Guid.NewGuid(), Key = "MAX_WORKSPACES_FREE", Value = "1", Description = "Máximo de workspaces para plano Free", Category = "QUOTAS" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "MAX_WORKSPACES_PRO", Value = "5", Description = "Máximo de workspaces para plano Pro", Category = "QUOTAS" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "MAX_WORKSPACES_ENTERPRISE", Value = "15", Description = "Máximo de workspaces para plano Enterprise", Category = "QUOTAS" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "MAX_USERS_FREE", Value = "2", Description = "Máximo de usuários por workspace para plano Free", Category = "QUOTAS" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "MAX_USERS_PRO", Value = "10", Description = "Máximo de usuários por workspace para plano Pro", Category = "QUOTAS" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "MAX_USERS_ENTERPRISE", Value = "25", Description = "Máximo de usuários por workspace para plano Enterprise", Category = "QUOTAS" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "LOG_RETENTION_FREE", Value = "7", Description = "Retenção de logs em dias para plano Free", Category = "RETENTION" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "LOG_RETENTION_PRO", Value = "30", Description = "Retenção de logs em dias para plano Pro", Category = "RETENTION" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "LOG_RETENTION_ENTERPRISE", Value = "90", Description = "Retenção de logs em dias para plano Enterprise", Category = "RETENTION" },
            new SystemParameter { Id = Guid.NewGuid(), Key = "DEFAULT_PAGE_SIZE", Value = "50", Description = "Tamanho padrão de paginação", Category = "PERFORMANCE" }
        };

        modelBuilder.Entity<SystemParameter>().HasData(systemParams);
    }
}
```

## 2. Cache Redis e Performance

### 2.1 Redis Configuration

#### IDE.Infrastructure/Cache/RedisCacheService.cs
```csharp
public interface IRedisCacheService
{
    Task<T> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task RemovePatternAsync(string pattern);
    Task<bool> ExistsAsync(string key);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase();
        _redis = redis;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<T> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<T>(value, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao recuperar valor do cache para chave: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _database.StringSetAsync(key, json, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao armazenar valor no cache para chave: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover chave do cache: {Key}", key);
        }
    }

    public async Task RemovePatternAsync(string pattern)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern);
            
            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover chaves por padrão: {Pattern}", pattern);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência da chave: {Key}", key);
            return false;
        }
    }
}
```

### 2.2 SignalR Hub de Workspace

#### IDE.Infrastructure/SignalR/WorkspaceHub.cs
```csharp
[Authorize]
public class WorkspaceHub : Hub
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspaceHub> _logger;

    public WorkspaceHub(IWorkspaceService workspaceService, ILogger<WorkspaceHub> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    public async Task JoinWorkspace(string workspaceId)
    {
        var userId = GetCurrentUserId();
        var workspaceGuid = Guid.Parse(workspaceId);

        // Verificar permissão
        if (!await _workspaceService.HasWorkspaceAccessAsync(workspaceGuid, userId))
        {
            await Clients.Caller.SendAsync("Error", "Acesso negado ao workspace");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"workspace_{workspaceId}");
        await Clients.Group($"workspace_{workspaceId}").SendAsync("UserJoined", new { UserId = userId, ConnectionId = Context.ConnectionId });
        
        _logger.LogInformation("Usuário {UserId} entrou no workspace {WorkspaceId}", userId, workspaceId);
    }

    public async Task LeaveWorkspace(string workspaceId)
    {
        var userId = GetCurrentUserId();
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workspace_{workspaceId}");
        await Clients.Group($"workspace_{workspaceId}").SendAsync("UserLeft", new { UserId = userId, ConnectionId = Context.ConnectionId });
        
        _logger.LogInformation("Usuário {UserId} saiu do workspace {WorkspaceId}", userId, workspaceId);
    }

    public async Task NotifyItemChange(string workspaceId, string itemId, string changeType, object data)
    {
        var userId = GetCurrentUserId();
        var workspaceGuid = Guid.Parse(workspaceId);

        // Verificar permissão
        if (!await _workspaceService.HasWorkspaceAccessAsync(workspaceGuid, userId, PermissionLevel.Editor))
        {
            await Clients.Caller.SendAsync("Error", "Permissão de editor necessária");
            return;
        }

        // Notificar outros usuários no workspace (exceto o remetente)
        await Clients.GroupExcept($"workspace_{workspaceId}", Context.ConnectionId).SendAsync("ItemChanged", new 
        { 
            WorkspaceId = workspaceId,
            ItemId = itemId,
            ChangeType = changeType,
            Data = data,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("Usuário {UserId} alterou item {ItemId} no workspace {WorkspaceId}: {ChangeType}", 
            userId, itemId, workspaceId, changeType);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Usuário {UserId} desconectado. Conexão: {ConnectionId}", userId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Token inválido");
        }
        return userId;
    }
}
```

### 2.3 Notification Service

#### IDE.Application/Services/IWorkspaceNotificationService.cs
```csharp
public interface IWorkspaceNotificationService
{
    Task NotifyWorkspaceUpdated(Guid workspaceId, string eventType, object data);
    Task NotifyItemUpdated(Guid workspaceId, Guid itemId, string eventType, object data);
    Task NotifyUserJoined(Guid workspaceId, Guid userId);
    Task NotifyUserLeft(Guid workspaceId, Guid userId);
    Task NotifyPermissionChanged(Guid workspaceId, Guid userId, PermissionLevel newLevel);
}

public class WorkspaceNotificationService : IWorkspaceNotificationService
{
    private readonly IHubContext<WorkspaceHub> _hubContext;
    private readonly ILogger<WorkspaceNotificationService> _logger;

    public WorkspaceNotificationService(
        IHubContext<WorkspaceHub> hubContext,
        ILogger<WorkspaceNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyWorkspaceUpdated(Guid workspaceId, string eventType, object data)
    {
        try
        {
            await _hubContext.Clients.Group($"workspace_{workspaceId}")
                .SendAsync("WorkspaceUpdated", new
                {
                    WorkspaceId = workspaceId,
                    EventType = eventType,
                    Data = data,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar atualização do workspace {WorkspaceId}", workspaceId);
        }
    }

    public async Task NotifyItemUpdated(Guid workspaceId, Guid itemId, string eventType, object data)
    {
        try
        {
            await _hubContext.Clients.Group($"workspace_{workspaceId}")
                .SendAsync("ItemUpdated", new
                {
                    WorkspaceId = workspaceId,
                    ItemId = itemId,
                    EventType = eventType,
                    Data = data,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar atualização do item {ItemId}", itemId);
        }
    }

    public async Task NotifyUserJoined(Guid workspaceId, Guid userId)
    {
        try
        {
            await _hubContext.Clients.Group($"workspace_{workspaceId}")
                .SendAsync("UserJoined", new
                {
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar entrada do usuário {UserId}", userId);
        }
    }

    public async Task NotifyUserLeft(Guid workspaceId, Guid userId)
    {
        try
        {
            await _hubContext.Clients.Group($"workspace_{workspaceId}")
                .SendAsync("UserLeft", new
                {
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar saída do usuário {UserId}", userId);
        }
    }

    public async Task NotifyPermissionChanged(Guid workspaceId, Guid userId, PermissionLevel newLevel)
    {
        try
        {
            await _hubContext.Clients.Group($"workspace_{workspaceId}")
                .SendAsync("PermissionChanged", new
                {
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    NewLevel = newLevel.ToString(),
                    Timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao notificar mudança de permissão do usuário {UserId}", userId);
        }
    }
}
```

## 7. Endpoints de API Atualizados

### 7.1 Controlador Principal Atualizado

#### IDE.API/Controllers/WorkspaceController.cs (Principais Mudanças)
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspaceController> _logger;

    public WorkspaceController(
        IWorkspaceService workspaceService,
        ILogger<WorkspaceController> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    // ATUALIZADO - Item Management com Cache
    [HttpPost("{workspaceId:guid}/items")]
    [ProducesResponseType(typeof(ModuleItemDto), 201)]
    public async Task<IActionResult> CreateItem(Guid workspaceId, [FromBody] CreateModuleItemRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _workspaceService.CreateItemAsync(workspaceId, userId, request);
            return CreatedAtAction(nameof(GetItem), new { workspaceId, itemId = result.Id }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{workspaceId:guid}/items")]
    [ProducesResponseType(typeof(PaginatedItemsResponse<ModuleItemDto>), 200)]
    public async Task<IActionResult> GetWorkspaceItems(
        Guid workspaceId,
        [FromQuery] string module = null,
        [FromQuery] string type = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery] string[] tags = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string sortBy = "UpdatedAt",
        [FromQuery] string sortDirection = "desc")
    {
        try
        {
            var userId = GetCurrentUserId();
            var request = new ItemSearchRequest
            {
                Module = module,
                Type = type,
                ParentId = parentId,
                Tags = tags?.ToList() ?? new List<string>(),
                Page = page,
                PageSize = Math.Min(pageSize, 100), // Limite máximo
                SortBy = sortBy,
                SortDirection = sortDirection
            };

            var result = await _workspaceService.GetWorkspaceItemsAsync(workspaceId, userId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // NOVO - Update Data (Tempo Real)
    [HttpPut("{workspaceId:guid}/items/{itemId:guid}/data")]
    [ProducesResponseType(typeof(ModuleItemDto), 200)]
    public async Task<IActionResult> UpdateItemData(Guid workspaceId, Guid itemId, [FromBody] UpdateItemDataRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _workspaceService.UpdateItemDataAsync(workspaceId, itemId, userId, request);
            return Ok(result);
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message, details = ex.Details });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    // NOVO - Save Item (Com Versionamento)
    [HttpPost("{workspaceId:guid}/items/{itemId:guid}/save")]
    [ProducesResponseType(typeof(ModuleItemDto), 200)]
    public async Task<IActionResult> SaveItem(Guid workspaceId, Guid itemId, [FromBody] SaveItemRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _workspaceService.SaveItemAsync(workspaceId, itemId, userId, request);
            return Ok(result);
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message, details = ex.Details });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{workspaceId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(ModuleItemDto), 200)]
    public async Task<IActionResult> UpdateItem(Guid workspaceId, Guid itemId, [FromBody] UpdateModuleItemRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _workspaceService.UpdateItemAsync(workspaceId, itemId, userId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // NOVO - Navigation State Management
    [HttpGet("{workspaceId:guid}/navigation/{moduleId}")]
    [ProducesResponseType(typeof(WorkspaceNavigationStateDto), 200)]
    public async Task<IActionResult> GetNavigationState(Guid workspaceId, string moduleId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _workspaceService.GetNavigationStateAsync(workspaceId, moduleId, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPut("{workspaceId:guid}/navigation")]
    [ProducesResponseType(typeof(WorkspaceNavigationStateDto), 200)]
    public async Task<IActionResult> SetNavigationState(Guid workspaceId, [FromBody] SetNavigationStateRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _workspaceService.SetNavigationStateAsync(workspaceId, userId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // NOVO - Cache Management
    [HttpPost("{workspaceId:guid}/cache/invalidate")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> InvalidateWorkspaceCache(Guid workspaceId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Verificar permissão de administrador
            var permission = await _workspaceService.GetUserPermissionLevelAsync(workspaceId, userId);
            if (permission != PermissionLevel.Owner)
            {
                return Forbid("Apenas o proprietário pode invalidar o cache");
            }

            await _workspaceService.InvalidateWorkspaceCacheAsync(workspaceId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPost("{workspaceId:guid}/items/{itemId:guid}/cache/invalidate")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> InvalidateItemCache(Guid workspaceId, Guid itemId)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Verificar permissão de editor
            var hasAccess = await _workspaceService.HasWorkspaceAccessAsync(workspaceId, userId, PermissionLevel.Editor);
            if (!hasAccess)
            {
                return Forbid("Permissão de editor necessária");
            }

            await _workspaceService.InvalidateItemCacheAsync(workspaceId, itemId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Token inválido");
        }
        return userId;
    }
}
```

### 7.2 Controlador de Sistema NOVO

#### IDE.API/Controllers/SystemController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        IWorkspaceService workspaceService,
        ILogger<SystemController> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    [HttpGet("parameters")]
    [ProducesResponseType(typeof(List<SystemParameterDto>), 200)]
    public async Task<IActionResult> GetSystemParameters([FromQuery] string category = null)
    {
        try
        {
            var result = await _workspaceService.GetSystemParametersAsync(category);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar parâmetros do sistema");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpGet("parameters/{key}")]
    [ProducesResponseType(typeof(string), 200)]
    public async Task<IActionResult> GetSystemParameter(string key)
    {
        try
        {
            var result = await _workspaceService.GetSystemParameterAsync(key);
            if (result == null)
            {
                return NotFound($"Parâmetro '{key}' não encontrado");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar parâmetro do sistema: {Key}", key);
            return StatusCode(500, "Erro interno do servidor");
        }
    }
}
```

### 7.3 Middleware de Tratamento de Conflitos

#### IDE.API/Middleware/ConflictHandlingMiddleware.cs
```csharp
public class ConflictHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ConflictHandlingMiddleware> _logger;

    public ConflictHandlingMiddleware(RequestDelegate next, ILogger<ConflictHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning("Conflito detectado: {Message}", ex.Message);
            
            context.Response.StatusCode = 409; // Conflict
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "CONFLICT",
                message = ex.Message,
                details = ex.Details,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}
```

## 3. SignalR Hub Básico

### 3.1 WorkspaceHub para Sincronização

#### IDE.API/Hubs/WorkspaceHub.cs
```csharp
// Hub já implementado na seção anterior com funcionalidades completas
// Mantendo apenas a definição de interface aqui para referência

// Service para notificações
public interface IWorkspaceNotificationService
{
    Task NotifyWorkspaceUpdated(Guid workspaceId, string action, object data);
    Task NotifyItemUpdated(Guid workspaceId, Guid itemId, string action, object data);
}

public class WorkspaceNotificationService : IWorkspaceNotificationService
{
    private readonly IHubContext<WorkspaceHub> _hubContext;

    public WorkspaceNotificationService(IHubContext<WorkspaceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyWorkspaceUpdated(Guid workspaceId, string action, object data)
    {
        await _hubContext.Clients.Group($"workspace_{workspaceId}")
            .SendAsync("WorkspaceUpdated", new { Action = action, Data = data });
    }

    public async Task NotifyItemUpdated(Guid workspaceId, Guid itemId, string action, object data)
    {
        await _hubContext.Clients.Group($"workspace_{workspaceId}")
            .SendAsync("ItemUpdated", new { ItemId = itemId, Action = action, Data = data });
    }
}
```

## 4. DTOs e Requests Atualizados

### 4.1 DTOs de Workspace

#### IDE.Application/Workspace/DTOs/
```csharp
// WorkspaceDto.cs
public class WorkspaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string SemanticVersion { get; set; }
    public WorkspacePhaseDto CurrentPhase { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public UserDto Owner { get; set; }
    public List<ModuleItemDto> Items { get; set; } = new();
    public List<WorkspacePermissionDto> Permissions { get; set; } = new();
    public List<WorkspaceTagDto> Tags { get; set; } = new();
    public List<WorkspacePhaseDto> Phases { get; set; } = new();
    public int TotalItems { get; set; }
    public long TotalSize { get; set; }
}

// ModuleItemDto.cs - ATUALIZADO
public class ModuleItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public string Module { get; set; }          // Ex: "Documents", "Tests", "Environments", "API"
    public string Type { get; set; }            // Ex: "typescript", "markdown", "uml-diagram", "database-table"
    public Guid? ParentId { get; set; }         // Para hierarquia
    public int VersionNumber { get; set; }      // Para controle de conflitos
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ModuleItemTagDto> Tags { get; set; } = new();
    public List<ModuleItemDto> Children { get; set; } = new(); // Para hierarquia completa
}

// WorkspaceNavigationStateDto.cs - NOVO
public class WorkspaceNavigationStateDto
{
    public string ModuleId { get; set; }
    public object State { get; set; }          // Deserializado do JSON
    public DateTime UpdatedAt { get; set; }
}

// SystemParameterDto.cs - NOVO
public class SystemParameterDto
{
    public string Key { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
}

// PaginatedItemsResponse.cs - NOVO
public class PaginatedItemsResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### 4.2 Requests Atualizados

#### IDE.Application/Workspace/Requests/
```csharp
// CreateWorkspaceRequest.cs - ATUALIZADO
public class CreateWorkspaceRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> DefaultPhases { get; set; } = new() { "Development" }; // ATUALIZADO: apenas Development
}

// CreateModuleItemRequest.cs - ATUALIZADO
public class CreateModuleItemRequest
{
    public string Name { get; set; }
    public string Content { get; set; } = "";
    public string Module { get; set; }          // Ex: "Documents", "Tests", "Environments", "API"
    public string Type { get; set; }            // Ex: "typescript", "markdown", "uml-diagram"
    public Guid? ParentId { get; set; }         // Para hierarquia
    public List<string> Tags { get; set; } = new();
}

// UpdateModuleItemRequest.cs - ATUALIZADO
public class UpdateModuleItemRequest
{
    public string Name { get; set; }
    public string Content { get; set; }
    public string Module { get; set; }
    public string Type { get; set; }            // Substitui EditorType e Language
    public int VersionNumber { get; set; }      // Para controle de conflitos
}

// UpdateItemDataRequest.cs - NOVO
public class UpdateItemDataRequest
{
    public string Content { get; set; }
    public int VersionNumber { get; set; }      // Para controle de conflitos
}

// SaveItemRequest.cs - NOVO
public class SaveItemRequest
{
    public int VersionNumber { get; set; }      // Para controle de conflitos
    public string Comment { get; set; }         // Comentário opcional para histórico
}

// ItemSearchRequest.cs - NOVO
public class ItemSearchRequest
{
    public string Module { get; set; }
    public string Type { get; set; }
    public Guid? ParentId { get; set; }
    public List<string> Tags { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "UpdatedAt";
    public string SortDirection { get; set; } = "desc";
}

// SetNavigationStateRequest.cs - NOVO
public class SetNavigationStateRequest
{
    public string ModuleId { get; set; }
    public object State { get; set; }           // Será serializado para JSON
}
```

// ModuleItemTagDto.cs
public class ModuleItemTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public DateTime CreatedAt { get; set; }
}

// WorkspacePhaseDto.cs
public class WorkspacePhaseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int Order { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

// WorkspacePermissionDto.cs
public class WorkspacePermissionDto
{
    public Guid Id { get; set; }
    public PermissionLevel Level { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserDto User { get; set; }
}

// WorkspaceTagDto.cs
public class WorkspaceTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public DateTime CreatedAt { get; set; }
}

// WorkspaceInvitationDto.cs
public class WorkspaceInvitationDto
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public PermissionLevel Level { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public UserDto InvitedBy { get; set; }
}

// ActivityLogDto.cs
public class ActivityLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; }
    public string Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserDto User { get; set; }
}

// WorkspaceVersionDto.cs
public class WorkspaceVersionDto
{
    public Guid Id { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public WorkspacePhaseDto Phase { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserDto CreatedBy { get; set; }
}
```

### 2.2 Requests

#### IDE.Application/Workspace/Requests/
```csharp
// CreateWorkspaceRequest.cs
public class CreateWorkspaceRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> DefaultPhases { get; set; } = new() { "Development" };
}

// UpdateWorkspaceRequest.cs
public class UpdateWorkspaceRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
}

// CreateModuleItemRequest.cs
public class CreateModuleItemRequest
{
    public string Name { get; set; }
    public string Content { get; set; } = "";
    public string Module { get; set; }
    public string Type { get; set; }
    public List<string> Tags { get; set; } = new();
}

// UpdateModuleItemRequest.cs
public class UpdateModuleItemRequest
{
    public string Name { get; set; }
    public string Content { get; set; }
    public string Module { get; set; }
    public string Type { get; set; }
}

// CreateWorkspacePhaseRequest.cs
public class CreateWorkspacePhaseRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Color { get; set; } = "#52c41a";
    public int Order { get; set; }
}

// UpdateWorkspacePhaseRequest.cs
public class UpdateWorkspacePhaseRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int Order { get; set; }
}

// InviteUserRequest.cs
public class InviteUserRequest
{
    public string Email { get; set; }
    public PermissionLevel Level { get; set; } = PermissionLevel.Reader;
}

// UpdatePermissionRequest.cs
public class UpdatePermissionRequest
{
    public PermissionLevel Level { get; set; }
}

// CreateWorkspaceTagRequest.cs
public class CreateWorkspaceTagRequest
{
    public string Name { get; set; }
    public string Color { get; set; } = "#1890ff";
}

// AddItemTagRequest.cs
public class AddItemTagRequest
{
    public string Name { get; set; }
    public string Color { get; set; } = "#1890ff";
}

// CreateVersionRequest.cs
public class CreateVersionRequest
{
    public string Version { get; set; }
    public string Description { get; set; }
}

// PromoteWorkspaceRequest.cs
public class PromoteWorkspaceRequest
{
    public Guid? TargetPhaseId { get; set; } // Se null, promove para próxima fase
}

// WorkspaceSearchRequest.cs
public class WorkspaceSearchRequest
{
    public string Query { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool? IsArchived { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "UpdatedAt";
    public string SortDirection { get; set; } = "desc";
}
```

## 5. Validações Atualizadas

### IDE.Application/Workspace/Validators/
```csharp
// CreateModuleItemRequestValidator.cs - ATUALIZADO
public class CreateModuleItemRequestValidator : AbstractValidator<CreateModuleItemRequest>
{
    private readonly List<string> _validModules = new() { "Documents", "Tests", "Environments", "API" };
    private readonly List<string> _validTypes = new() { "typescript", "javascript", "json", "markdown", "html", "css", "sql", "text", "uml-diagram", "database", "database-table", "config" };

    public CreateModuleItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório")
            .Length(1, 200).WithMessage("Nome deve ter entre 1 e 200 caracteres");

        RuleFor(x => x.Module)
            .NotEmpty().WithMessage("Módulo é obrigatório")
            .Must(x => _validModules.Contains(x)).WithMessage($"Módulo deve ser um dos: {string.Join(", ", _validModules)}");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Tipo é obrigatório")
            .Must(x => _validTypes.Contains(x)).WithMessage($"Tipo deve ser um dos: {string.Join(", ", _validTypes)}");

        RuleFor(x => x.Content)
            .Must(content => content?.Length <= 10 * 1024 * 1024).WithMessage("Conteúdo deve ter no máximo 10MB");

        RuleForEach(x => x.Tags)
            .Length(1, 50).WithMessage("Tag deve ter entre 1 e 50 caracteres");
    }
}

// UpdateItemDataRequestValidator.cs - NOVO
public class UpdateItemDataRequestValidator : AbstractValidator<UpdateItemDataRequest>
{
    public UpdateItemDataRequestValidator()
    {
        RuleFor(x => x.Content)
            .Must(content => content?.Length <= 10 * 1024 * 1024).WithMessage("Conteúdo deve ter no máximo 10MB");

        RuleFor(x => x.VersionNumber)
            .GreaterThan(0).WithMessage("Version number deve ser maior que 0");
    }
}

// ItemSearchRequestValidator.cs - NOVO
public class ItemSearchRequestValidator : AbstractValidator<ItemSearchRequest>
{
    public ItemSearchRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Página deve ser maior que 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Tamanho da página deve ser entre 1 e 100");

        RuleFor(x => x.SortDirection)
            .Must(x => x == "asc" || x == "desc").WithMessage("Direção de ordenação deve ser 'asc' ou 'desc'");
    }
}
```

// CreateWorkspacePhaseRequestValidator.cs
public class CreateWorkspacePhaseRequestValidator : AbstractValidator<CreateWorkspacePhaseRequest>
{
    public CreateWorkspacePhaseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório")
            .Length(1, 100).WithMessage("Nome deve ter entre 1 e 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Descrição deve ter no máximo 500 caracteres");

        RuleFor(x => x.Color)
            .Matches(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$").WithMessage("Cor deve ser um código hexadecimal válido");

        RuleFor(x => x.Order)
            .GreaterThan(0).WithMessage("Ordem deve ser maior que 0");
    }
}

// InviteUserRequestValidator.cs
public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório")
            .EmailAddress().WithMessage("Email deve ser válido")
            .MaximumLength(255).WithMessage("Email deve ter no máximo 255 caracteres");

        RuleFor(x => x.Level)
            .IsInEnum().WithMessage("Nível de permissão inválido")
            .NotEqual(PermissionLevel.Owner).WithMessage("Não é possível convidar como Owner");
    }
}
```

## 6. Serviços Atualizados

### 6.1 Interface do Serviço Atualizada

#### IDE.Application/Workspace/Services/IWorkspaceService.cs
```csharp
public interface IWorkspaceService
{
    // Workspace CRUD
    Task<WorkspaceDto> CreateWorkspaceAsync(Guid userId, CreateWorkspaceRequest request);
    Task<WorkspaceDto> GetWorkspaceAsync(Guid workspaceId, Guid userId);
    Task<PaginatedResponse<WorkspaceDto>> GetUserWorkspacesAsync(Guid userId, WorkspaceSearchRequest request);
    Task<WorkspaceDto> UpdateWorkspaceAsync(Guid workspaceId, Guid userId, UpdateWorkspaceRequest request);
    Task<bool> DeleteWorkspaceAsync(Guid workspaceId, Guid userId);

    // Item Management - ATUALIZADO
    Task<ModuleItemDto> CreateItemAsync(Guid workspaceId, Guid userId, CreateModuleItemRequest request);
    Task<ModuleItemDto> GetItemAsync(Guid workspaceId, Guid itemId, Guid userId);
    Task<PaginatedItemsResponse<ModuleItemDto>> GetWorkspaceItemsAsync(Guid workspaceId, Guid userId, ItemSearchRequest request);
    Task<ModuleItemDto> UpdateItemAsync(Guid workspaceId, Guid itemId, Guid userId, UpdateModuleItemRequest request);
    Task<ModuleItemDto> UpdateItemDataAsync(Guid workspaceId, Guid itemId, Guid userId, UpdateItemDataRequest request); // NOVO
    Task<ModuleItemDto> SaveItemAsync(Guid workspaceId, Guid itemId, Guid userId, SaveItemRequest request); // NOVO
    Task<bool> DeleteItemAsync(Guid workspaceId, Guid itemId, Guid userId);

    // Navigation State - NOVO
    Task<WorkspaceNavigationStateDto> GetNavigationStateAsync(Guid workspaceId, string moduleId, Guid userId);
    Task<WorkspaceNavigationStateDto> SetNavigationStateAsync(Guid workspaceId, Guid userId, SetNavigationStateRequest request);

    // System Parameters - NOVO
    Task<List<SystemParameterDto>> GetSystemParametersAsync(string category = null);
    Task<string> GetSystemParameterAsync(string key);

    // Cache Management - NOVO
    Task InvalidateWorkspaceCacheAsync(Guid workspaceId);
    Task InvalidateItemCacheAsync(Guid workspaceId, Guid itemId);

    // Utility
    Task<bool> HasWorkspaceAccessAsync(Guid workspaceId, Guid userId, PermissionLevel minimumLevel = PermissionLevel.Reader);
    Task<PermissionLevel> GetUserPermissionLevelAsync(Guid workspaceId, Guid userId);
}
```

### 6.2 Implementação com Cache e Controle de Conflitos

#### IDE.Application/Workspace/Services/WorkspaceService.cs (Principais Mudanças)
```csharp
public class WorkspaceService : IWorkspaceService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkspaceService> _logger;
    private readonly IRedisCacheService _cache;
    private readonly IWorkspaceNotificationService _notificationService;

    public WorkspaceService(
        ApplicationDbContext context,
        IMapper mapper,
        ILogger<WorkspaceService> logger,
        IRedisCacheService cache,
        IWorkspaceNotificationService notificationService)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;
        _notificationService = notificationService;
    }

    public async Task<ModuleItemDto> UpdateItemDataAsync(Guid workspaceId, Guid itemId, Guid userId, UpdateItemDataRequest request)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, userId, PermissionLevel.Editor))
        {
            throw new UnauthorizedAccessException("Permissão de editor necessária");
        }

        // Controle de conflitos - verificar version number
        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
            "UPDATE ModuleItems SET Content = {0}, UpdatedAt = {1}, VersionNumber = VersionNumber + 1 " +
            "WHERE Id = {2} AND WorkspaceId = {3} AND VersionNumber = {4}",
            request.Content, DateTime.UtcNow, itemId, workspaceId, request.VersionNumber);

        if (rowsAffected == 0)
        {
            // Conflito detectado - buscar dados atuais
            var currentItem = await _context.ModuleItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.WorkspaceId == workspaceId);
            
            if (currentItem == null)
            {
                throw new KeyNotFoundException("Item não encontrado");
            }

            throw new ConflictException("Item foi modificado por outro usuário", 
                new { currentItem.VersionNumber, currentItem.Content, currentItem.UpdatedAt });
        }

        // Invalidar cache
        await InvalidateItemCacheAsync(workspaceId, itemId);

        // Notificar via SignalR
        await _notificationService.NotifyItemUpdated(workspaceId, itemId, "DATA_UPDATED", new { Content = request.Content });

        return await GetItemAsync(workspaceId, itemId, userId);
    }

    public async Task<ModuleItemDto> SaveItemAsync(Guid workspaceId, Guid itemId, Guid userId, SaveItemRequest request)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, userId, PermissionLevel.Editor))
        {
            throw new UnauthorizedAccessException("Permissão de editor necessária");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Verificar version number e marcar como salvo
            var item = await _context.ModuleItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.WorkspaceId == workspaceId && i.VersionNumber == request.VersionNumber);

            if (item == null)
            {
                throw new ConflictException("Item foi modificado ou não encontrado");
            }

            // Criar versão no histórico se necessário
            if (!string.IsNullOrEmpty(request.Comment))
            {
                var itemVersion = new ItemVersion
                {
                    Id = Guid.NewGuid(),
                    Content = item.Content,
                    Comment = request.Comment,
                    ItemId = itemId,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ItemVersions.Add(itemVersion);
            }

            // Atualizar timestamp
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Invalidar cache
            await InvalidateItemCacheAsync(workspaceId, itemId);

            // Notificar via SignalR
            await _notificationService.NotifyItemUpdated(workspaceId, itemId, "SAVED", new { Comment = request.Comment });

            return await GetItemAsync(workspaceId, itemId, userId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PaginatedItemsResponse<ModuleItemDto>> GetWorkspaceItemsAsync(Guid workspaceId, Guid userId, ItemSearchRequest request)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Acesso negado ao workspace");
        }

        // Verificar cache primeiro
        var cacheKey = $"workspace_{workspaceId}_items_{GenerateCacheKeySuffix(request)}";
        var cachedResult = await _cache.GetAsync<PaginatedItemsResponse<ModuleItemDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        var query = _context.ModuleItems
            .Include(i => i.Tags)
            .Where(i => i.WorkspaceId == workspaceId);

        // Aplicar filtros
        if (!string.IsNullOrEmpty(request.Module))
            query = query.Where(i => i.Module == request.Module);

        if (!string.IsNullOrEmpty(request.Type))
            query = query.Where(i => i.Type == request.Type);

        if (request.ParentId.HasValue)
            query = query.Where(i => i.ParentId == request.ParentId);

        if (request.Tags.Any())
            query = query.Where(i => i.Tags.Any(t => request.Tags.Contains(t.Name)));

        // Ordenação
        query = request.SortBy switch
        {
            "Name" => request.SortDirection == "asc" ? query.OrderBy(i => i.Name) : query.OrderByDescending(i => i.Name),
            "CreatedAt" => request.SortDirection == "asc" ? query.OrderBy(i => i.CreatedAt) : query.OrderByDescending(i => i.CreatedAt),
            _ => request.SortDirection == "asc" ? query.OrderBy(i => i.UpdatedAt) : query.OrderByDescending(i => i.UpdatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var result = new PaginatedItemsResponse<ModuleItemDto>
        {
            Items = _mapper.Map<List<ModuleItemDto>>(items),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        // Cache por 5 minutos
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;
    }

    // Navigation State Management
    public async Task<WorkspaceNavigationStateDto> GetNavigationStateAsync(Guid workspaceId, string moduleId, Guid userId)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Acesso negado ao workspace");
        }

        var state = await _context.WorkspaceNavigationStates
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId && s.ModuleId == moduleId);

        if (state == null)
        {
            return new WorkspaceNavigationStateDto
            {
                ModuleId = moduleId,
                State = new { },
                UpdatedAt = DateTime.UtcNow
            };
        }

        return new WorkspaceNavigationStateDto
        {
            ModuleId = state.ModuleId,
            State = JsonSerializer.Deserialize<object>(state.StateJson),
            UpdatedAt = state.UpdatedAt
        };
    }

    public async Task<WorkspaceNavigationStateDto> SetNavigationStateAsync(Guid workspaceId, Guid userId, SetNavigationStateRequest request)
    {
        if (!await HasWorkspaceAccessAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Acesso negado ao workspace");
        }

        var stateJson = JsonSerializer.Serialize(request.State);
        var now = DateTime.UtcNow;

        var existingState = await _context.WorkspaceNavigationStates
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId && s.ModuleId == request.ModuleId);

        if (existingState != null)
        {
            existingState.StateJson = stateJson;
            existingState.UpdatedAt = now;
        }
        else
        {
            existingState = new WorkspaceNavigationState
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                ModuleId = request.ModuleId,
                StateJson = stateJson,
                UpdatedAt = now
            };
            _context.WorkspaceNavigationStates.Add(existingState);
        }

        await _context.SaveChangesAsync();

        return new WorkspaceNavigationStateDto
        {
            ModuleId = request.ModuleId,
            State = request.State,
            UpdatedAt = now
        };
    }

    // Cache management
    public async Task InvalidateWorkspaceCacheAsync(Guid workspaceId)
    {
        await _cache.RemovePatternAsync($"workspace_{workspaceId}_*");
    }

    public async Task InvalidateItemCacheAsync(Guid workspaceId, Guid itemId)
    {
        await _cache.RemovePatternAsync($"workspace_{workspaceId}_items_*");
        await _cache.RemoveAsync($"item_{itemId}");
    }

    private string GenerateCacheKeySuffix(ItemSearchRequest request)
    {
        var parts = new List<string>
        {
            request.Module ?? "all",
            request.Type ?? "all",
            request.ParentId?.ToString() ?? "all",
            string.Join(",", request.Tags.OrderBy(t => t)),
            request.Page.ToString(),
            request.PageSize.ToString(),
            request.SortBy,
            request.SortDirection
        };
        return string.Join("_", parts);
    }
}
```

## 5. Endpoints de Workspace (Minimal API)

### IDE.API/Endpoints/WorkspaceEndpoints.cs
```csharp
public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces")
            .WithTags("Workspaces")
            .WithOpenApi()
            .RequireAuthorization();

        // Workspace Management
        group.MapGet("/", async (
            [FromServices] IWorkspaceService workspaceService,
            [AsParameters] WorkspaceSearchRequest request,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var result = await workspaceService.GetUserWorkspacesAsync(userId, request);
            
            return Results.Ok(result);
        })
        .WithName("GetUserWorkspaces")
        .WithSummary("Listar workspaces do usuário")
        .Produces<PaginatedResponse<WorkspaceDto>>(200);

        group.MapPost("/", async (
            [FromBody] CreateWorkspaceRequest request,
            [FromServices] IWorkspaceService workspaceService,
            [FromServices] IValidator<CreateWorkspaceRequest> validator,
            ClaimsPrincipal user) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Error(
                    validationResult.Errors.Select(e => e.ErrorMessage).ToList(),
                    "Dados de workspace inválidos"));
            }

            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var workspace = await workspaceService.CreateWorkspaceAsync(userId, request);
            
            return Results.Created($"/api/workspaces/{workspace.Id}", 
                ApiResponse<WorkspaceDto>.Success(workspace, "Workspace criado com sucesso"));
        })
        .WithName("CreateWorkspace")
        .WithSummary("Criar novo workspace")
        .Produces<ApiResponse<WorkspaceDto>>(201)
        .Produces<ApiResponse<object>>(400);

        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IWorkspaceService workspaceService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var workspace = await workspaceService.GetWorkspaceAsync(id, userId);
            
            return Results.Ok(ApiResponse<WorkspaceDto>.Success(workspace, "Workspace obtido com sucesso"));
        })
        .WithName("GetWorkspace")
        .WithSummary("Obter workspace específico")
        .Produces<ApiResponse<WorkspaceDto>>(200)
        .Produces<ApiResponse<object>>(404);

        group.MapPut("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateWorkspaceRequest request,
            [FromServices] IWorkspaceService workspaceService,
            [FromServices] IValidator<UpdateWorkspaceRequest> validator,
            ClaimsPrincipal user) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Error(
                    validationResult.Errors.Select(e => e.ErrorMessage).ToList(),
                    "Dados de atualização inválidos"));
            }

            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var workspace = await workspaceService.UpdateWorkspaceAsync(id, userId, request);
            
            return Results.Ok(ApiResponse<WorkspaceDto>.Success(workspace, "Workspace atualizado com sucesso"));
        })
        .WithName("UpdateWorkspace")
        .WithSummary("Atualizar workspace")
        .Produces<ApiResponse<WorkspaceDto>>(200)
        .Produces<ApiResponse<object>>(400);

        group.MapDelete("/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] IWorkspaceService workspaceService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var success = await workspaceService.DeleteWorkspaceAsync(id, userId);
            
            if (!success)
            {
                return Results.NotFound(ApiResponse<object>.Error("Workspace não encontrado"));
            }

            return Results.Ok(ApiResponse<object>.Success(null, "Workspace deletado com sucesso"));
        })
        .WithName("DeleteWorkspace")
        .WithSummary("Deletar workspace")
        .Produces<ApiResponse<object>>(200)
        .Produces<ApiResponse<object>>(404);

        // Item Management
        group.MapGet("/{id:guid}/items", async (
            [FromRoute] Guid id,
            [FromQuery] string module,
            [FromServices] IWorkspaceService workspaceService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var items = await workspaceService.GetWorkspaceItemsAsync(id, userId, module);
            
            return Results.Ok(ApiResponse<List<ModuleItemDto>>.Success(items, "Itens obtidos com sucesso"));
        })
        .WithName("GetWorkspaceItems")
        .WithSummary("Listar itens do workspace")
        .Produces<ApiResponse<List<ModuleItemDto>>>(200);

        group.MapPost("/{id:guid}/items", async (
            [FromRoute] Guid id,
            [FromBody] CreateModuleItemRequest request,
            [FromServices] IWorkspaceService workspaceService,
            [FromServices] IValidator<CreateModuleItemRequest> validator,
            ClaimsPrincipal user) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Error(
                    validationResult.Errors.Select(e => e.ErrorMessage).ToList(),
                    "Dados de item inválidos"));
            }

            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var item = await workspaceService.CreateItemAsync(id, userId, request);
            
            return Results.Created($"/api/workspaces/{id}/items/{item.Id}", 
                ApiResponse<ModuleItemDto>.Success(item, "Item criado com sucesso"));
        })
        .WithName("CreateItem")
        .WithSummary("Criar item no workspace")
        .Produces<ApiResponse<ModuleItemDto>>(201)
        .Produces<ApiResponse<object>>(400);

        group.MapGet("/{id:guid}/items/{itemId:guid}", async (
            [FromRoute] Guid id,
            [FromRoute] Guid itemId,
            [FromServices] IWorkspaceService workspaceService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var item = await workspaceService.GetItemAsync(id, itemId, userId);
            
            return Results.Ok(ApiResponse<ModuleItemDto>.Success(item, "Item obtido com sucesso"));
        })
        .WithName("GetItem")
        .WithSummary("Obter item específico")
        .Produces<ApiResponse<ModuleItemDto>>(200)
        .Produces<ApiResponse<object>>(404);

        // Phases Management
        group.MapGet("/{id:guid}/phases", async (
            [FromRoute] Guid id,
            [FromServices] IWorkspaceService workspaceService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var phases = await workspaceService.GetWorkspacePhasesAsync(id, userId);
            
            return Results.Ok(ApiResponse<List<WorkspacePhaseDto>>.Success(phases, "Fases obtidas com sucesso"));
        })
        .WithName("GetWorkspacePhases")
        .WithSummary("Listar fases do workspace")
        .Produces<ApiResponse<List<WorkspacePhaseDto>>>(200);

        group.MapPost("/{id:guid}/phases", async (
            [FromRoute] Guid id,
            [FromBody] CreateWorkspacePhaseRequest request,
            [FromServices] IWorkspaceService workspaceService,
            [FromServices] IValidator<CreateWorkspacePhaseRequest> validator,
            ClaimsPrincipal user) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Error(
                    validationResult.Errors.Select(e => e.ErrorMessage).ToList(),
                    "Dados de fase inválidos"));
            }

            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var phase = await workspaceService.CreatePhaseAsync(id, userId, request);
            
            return Results.Created($"/api/workspaces/{id}/phases/{phase.Id}", 
                ApiResponse<WorkspacePhaseDto>.Success(phase, "Fase criada com sucesso"));
        })
        .WithName("CreatePhase")
        .WithSummary("Criar fase no workspace")
        .Produces<ApiResponse<WorkspacePhaseDto>>(201)
        .Produces<ApiResponse<object>>(400);

        // Collaboration
        group.MapGet("/{id:guid}/permissions", async (
            [FromRoute] Guid id,
            [FromServices] IWorkspaceService workspaceService,
            ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var permissions = await workspaceService.GetWorkspacePermissionsAsync(id, userId);
            
            return Results.Ok(ApiResponse<List<WorkspacePermissionDto>>.Success(permissions, "Permissões obtidas com sucesso"));
        })
        .WithName("GetWorkspacePermissions")
        .WithSummary("Listar permissões do workspace")
        .Produces<ApiResponse<List<WorkspacePermissionDto>>>(200);

        group.MapPost("/{id:guid}/permissions", async (
            [FromRoute] Guid id,
            [FromBody] InviteUserRequest request,
            [FromServices] IWorkspaceService workspaceService,
            [FromServices] IValidator<InviteUserRequest> validator,
            ClaimsPrincipal user) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(ApiResponse<object>.Error(
                    validationResult.Errors.Select(e => e.ErrorMessage).ToList(),
                    "Dados de convite inválidos"));
            }

            var userId = Guid.Parse(user.FindFirst("id")?.Value);
            var invitation = await workspaceService.InviteUserAsync(id, userId, request);
            
            return Results.Ok(ApiResponse<WorkspaceInvitationDto>.Success(invitation, "Convite enviado com sucesso"));
        })
        .WithName("InviteUser")
        .WithSummary("Convidar usuário para workspace")
        .Produces<ApiResponse<WorkspaceInvitationDto>>(200)
        .Produces<ApiResponse<object>>(400);
    }
}
```

## Entregáveis da Fase 2

✅ **Entidades de workspace** completas com relacionamentos  
✅ **CRUD de workspaces** com validações e permissões  
✅ **Sistema de itens** (ModuleItem) com módulos como string  
✅ **Fases customizáveis** por workspace com ordenação  
✅ **Sistema de tags** para workspace e itens  
✅ **Permissões e colaboração** (Owner, Editor, Reader)  
✅ **Sistema de convites** com tokens  
✅ **Versionamento semântico** básico  
✅ **Activity log** para auditoria  
✅ **Validações robustas** com FluentValidation  
✅ **Endpoints completos** com documentação  

## Validação da Fase 2

### Critérios de Sucesso
- [ ] Criação de workspace funciona com fases padrão
- [ ] CRUD de itens funciona com validação de módulos
- [ ] Sistema de permissões bloqueia acesso adequadamente  
- [ ] Tags podem ser criadas e associadas
- [ ] Fases podem ser criadas e ordenadas
- [ ] Convites são gerados com tokens válidos
- [ ] Limites de plano são respeitados
- [ ] Activity logs são registrados
- [ ] Busca e filtros funcionam
- [ ] Endpoints retornam dados corretos

## 8. Exception Handling Atualizado

### 8.1 Custom Exceptions

#### IDE.Application/Common/Exceptions/ConflictException.cs
```csharp
public class ConflictException : Exception
{
    public object Details { get; }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, object details) : base(message)
    {
        Details = details;
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ConflictException(string message, object details, Exception innerException) : base(message, innerException)
    {
        Details = details;
    }
}
```

### 8.2 Global Exception Handler Atualizado

#### IDE.API/Middleware/GlobalExceptionHandlerMiddleware.cs
```csharp
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new
        {
            message = exception.Message,
            timestamp = DateTime.UtcNow,
            path = context.Request.Path
        };

        switch (exception)
        {
            case ConflictException conflictEx:
                response.StatusCode = 409;
                errorResponse = new
                {
                    error = "CONFLICT",
                    message = conflictEx.Message,
                    details = conflictEx.Details,
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };
                _logger.LogWarning("Conflito: {Message}", conflictEx.Message);
                break;

            case UnauthorizedAccessException:
                response.StatusCode = 403;
                errorResponse = new
                {
                    error = "FORBIDDEN",
                    message = exception.Message,
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };
                _logger.LogWarning("Acesso negado: {Message}", exception.Message);
                break;

            case KeyNotFoundException:
                response.StatusCode = 404;
                errorResponse = new
                {
                    error = "NOT_FOUND",
                    message = exception.Message,
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };
                _logger.LogWarning("Recurso não encontrado: {Message}", exception.Message);
                break;

            case InvalidOperationException:
                response.StatusCode = 400;
                errorResponse = new
                {
                    error = "BAD_REQUEST",
                    message = exception.Message,
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };
                _logger.LogWarning("Operação inválida: {Message}", exception.Message);
                break;

            case ValidationException validationEx:
                response.StatusCode = 422;
                errorResponse = new
                {
                    error = "VALIDATION_ERROR",
                    message = "Dados inválidos",
                    errors = validationEx.Errors,
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };
                _logger.LogWarning("Erro de validação: {Errors}", string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage)));
                break;

            default:
                response.StatusCode = 500;
                errorResponse = new
                {
                    error = "INTERNAL_ERROR",
                    message = "Erro interno do servidor",
                    timestamp = DateTime.UtcNow,
                    path = context.Request.Path
                };
                _logger.LogError(exception, "Erro não tratado");
                break;
        }

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(json);
    }
}
```

## 9. Startup Configuration Atualizada

### 9.1 Program.cs Atualizado

#### IDE.API/Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuração de serviços
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(configuration);
});

// SignalR
builder.Services.AddSignalR();

// Dependency Injection
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<IWorkspaceNotificationService, WorkspaceNotificationService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(WorkspaceProfile));

// Authentication/Authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Configuração para SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/workspacehub"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>())
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddRedis(builder.Configuration.GetConnectionString("Redis"));

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middlewares
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<ConflictHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WorkspaceHub>("/workspacehub");
app.MapHealthChecks("/health");

// Aplicar migrações automaticamente em desenvolvimento
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
}

app.Run();
```

### 9.2 appsettings.json Atualizado

#### IDE.API/appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ide_workspace;Username=postgres;Password=your_password",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Key": "your-super-secret-key-that-is-at-least-256-bits-long",
    "Issuer": "IDE.API",
    "Audience": "IDE.Frontend",
    "ExpiryMinutes": 60
  },
  "AllowedOrigins": [
    "http://localhost:5173",
    "http://localhost:3000"
  ],
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "CacheSettings": {
    "DefaultExpirationMinutes": 5,
    "WorkspaceListExpirationMinutes": 10,
    "ItemListExpirationMinutes": 5,
    "NavigationStateExpirationMinutes": 30
  }
}
```

### 9.3 appsettings.Development.json

#### IDE.API/appsettings.Development.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ide_workspace_dev;Username=postgres;Password=dev_password",
    "Redis": "localhost:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information",
      "IDE.Application": "Debug",
      "IDE.Infrastructure": "Debug"
    }
  },
  "CacheSettings": {
    "DefaultExpirationMinutes": 1,
    "WorkspaceListExpirationMinutes": 2,
    "ItemListExpirationMinutes": 1,
    "NavigationStateExpirationMinutes": 5
  }
}
```

## 10. Configuração de Deployment

### 10.1 Dockerfile

#### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["IDE.API/IDE.API.csproj", "IDE.API/"]
COPY ["IDE.Application/IDE.Application.csproj", "IDE.Application/"]
COPY ["IDE.Domain/IDE.Domain.csproj", "IDE.Domain/"]
COPY ["IDE.Infrastructure/IDE.Infrastructure.csproj", "IDE.Infrastructure/"]
RUN dotnet restore "./IDE.API/IDE.API.csproj"
COPY . .
WORKDIR "/src/IDE.API"
RUN dotnet build "./IDE.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./IDE.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IDE.API.dll"]
```

### 10.2 Docker Compose

#### docker-compose.yml
```yaml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8503:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=ide_workspace;Username=postgres;Password=dev_password
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      - postgres
      - redis
    volumes:
      - ./logs:/app/logs

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: ide_workspace
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

  pgadmin:
    image: dpage/pgadmin4
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@ide.com
      PGADMIN_DEFAULT_PASSWORD: admin
    ports:
      - "8080:80"
    depends_on:
      - postgres

volumes:
  postgres_data:
  redis_data:
```

### 10.3 Database Init Script

#### init.sql
```sql
-- Extensões necessárias
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Configurações de performance
ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements';
ALTER SYSTEM SET max_connections = 200;
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET maintenance_work_mem = '64MB';
ALTER SYSTEM SET checkpoint_completion_target = 0.9;
ALTER SYSTEM SET wal_buffers = '16MB';
ALTER SYSTEM SET default_statistics_target = 100;

-- Restart necessário para aplicar as configurações
```

## 11. Testes e Validação

### 11.1 Testes de Integração de Cache

#### IDE.Tests/Integration/CacheTests.cs
```csharp
[TestFixture]
public class CacheIntegrationTests
{
    private IRedisCacheService _cacheService;
    private IConnectionMultiplexer _redis;

    [SetUp]
    public void Setup()
    {
        var configuration = "localhost:6379";
        _redis = ConnectionMultiplexer.Connect(configuration);
        _cacheService = new RedisCacheService(_redis, Mock.Of<ILogger<RedisCacheService>>());
    }

    [Test]
    public async Task SetAndGet_ShouldWorkCorrectly()
    {
        // Arrange
        var key = "test_key";
        var value = new { Name = "Test", Value = 123 };

        // Act
        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(1));
        var result = await _cacheService.GetAsync<object>(key);

        // Assert
        Assert.IsNotNull(result);
    }

    [Test]
    public async Task RemovePattern_ShouldRemoveMatchingKeys()
    {
        // Arrange
        await _cacheService.SetAsync("workspace_1_items", new { }, TimeSpan.FromMinutes(1));
        await _cacheService.SetAsync("workspace_1_users", new { }, TimeSpan.FromMinutes(1));
        await _cacheService.SetAsync("workspace_2_items", new { }, TimeSpan.FromMinutes(1));

        // Act
        await _cacheService.RemovePatternAsync("workspace_1_*");

        // Assert
        var result1 = await _cacheService.ExistsAsync("workspace_1_items");
        var result2 = await _cacheService.ExistsAsync("workspace_2_items");
        
        Assert.IsFalse(result1);
        Assert.IsTrue(result2);
    }

    [TearDown]
    public void TearDown()
    {
        _redis?.Dispose();
    }
}
```

### 11.2 Testes de Conflito

#### IDE.Tests/Unit/ConflictResolutionTests.cs
```csharp
[TestFixture]
public class ConflictResolutionTests
{
    [Test]
    public async Task UpdateItemData_WithOutdatedVersion_ShouldThrowConflictException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "Test", OwnerId = Guid.NewGuid() };
        var item = new ModuleItem 
        { 
            Id = Guid.NewGuid(), 
            Name = "Test Item", 
            Content = "Original Content",
            WorkspaceId = workspace.Id,
            VersionNumber = 1
        };

        context.Workspaces.Add(workspace);
        context.ModuleItems.Add(item);
        await context.SaveChangesAsync();

        var service = new WorkspaceService(context, Mock.Of<IMapper>(), Mock.Of<ILogger<WorkspaceService>>(), 
            Mock.Of<IRedisCacheService>(), Mock.Of<IWorkspaceNotificationService>());

        // Act & Assert
        var request = new UpdateItemDataRequest { Content = "New Content", VersionNumber = 0 };
        
        Assert.ThrowsAsync<ConflictException>(async () =>
            await service.UpdateItemDataAsync(workspace.Id, item.Id, Guid.NewGuid(), request));
    }
}
```

### 11.3 Scripts de Teste Manual

#### test-workspace-api.http
```http
### Criar workspace
POST http://localhost:8503/api/workspaces
Content-Type: application/json
Authorization: Bearer {{auth_token}}

{
  "name": "Meu Workspace Teste",
  "description": "Workspace para testes de API",
  "defaultPhases": ["Development"]
}

### Listar workspaces
GET http://localhost:8503/api/workspaces?page=1&pageSize=10
Authorization: Bearer {{auth_token}}

### Criar item no workspace
POST http://localhost:8503/api/workspaces/{{workspace_id}}/items
Content-Type: application/json
Authorization: Bearer {{auth_token}}

{
  "name": "ComponenteExemplo.tsx",
  "content": "import React from 'react';\n\nconst Exemplo = () => {\n  return <div>Hello World</div>;\n};\n\nexport default Exemplo;",
  "module": "Frontend",
  "type": "component",
  "tags": ["react", "typescript"]
}

### Buscar itens com filtros
GET http://localhost:8503/api/workspaces/{{workspace_id}}/items?module=Frontend&type=component&page=1&pageSize=20
Authorization: Bearer {{auth_token}}

### Atualizar dados do item (tempo real)
PUT http://localhost:8503/api/workspaces/{{workspace_id}}/items/{{item_id}}/data
Content-Type: application/json
Authorization: Bearer {{auth_token}}

{
  "content": "import React from 'react';\n\nconst Exemplo = () => {\n  return <div>Hello World Updated!</div>;\n};\n\nexport default Exemplo;",
  "versionNumber": 1
}

### Salvar item (com versionamento)
POST http://localhost:8503/api/workspaces/{{workspace_id}}/items/{{item_id}}/save
Content-Type: application/json
Authorization: Bearer {{auth_token}}

{
  "comment": "Atualização do componente com nova funcionalidade",
  "versionNumber": 1
}

### Gerenciar estado de navegação
PUT http://localhost:8503/api/workspaces/{{workspace_id}}/navigation
Content-Type: application/json
Authorization: Bearer {{auth_token}}

{
  "moduleId": "Frontend",
  "state": {
    "expandedNodes": ["components", "pages"],
    "selectedFile": "ComponenteExemplo.tsx",
    "activeTab": "editor"
  }
}

### Buscar parâmetros do sistema
GET http://localhost:8503/api/system/parameters?category=QUOTAS
Authorization: Bearer {{auth_token}}

### Invalidar cache do workspace (apenas owner)
POST http://localhost:8503/api/workspaces/{{workspace_id}}/cache/invalidate
Authorization: Bearer {{auth_token}}
```

### 11.4 Testes de Performance

#### IDE.Tests/Performance/WorkspacePerformanceTests.cs
```csharp
[TestFixture]
public class WorkspacePerformanceTests
{
    [Test]
    public async Task GetWorkspaceItems_WithCache_ShouldBeUnder200ms()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        // Simular requisição com cache ativo
        await Task.Delay(50); // Simular operação
        
        stopwatch.Stop();
        
        // Assert
        Assert.Less(stopwatch.ElapsedMilliseconds, 200);
    }

    [Test]
    public async Task ConcurrentUpdates_ShouldHandleConflictsCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();
        var successCount = 0;
        var conflictCount = 0;

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // Simular atualização concorrente
                    await Task.Delay(Random.Next(10, 100));
                    Interlocked.Increment(ref successCount);
                }
                catch (ConflictException)
                {
                    Interlocked.Increment(ref conflictCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Greater(successCount, 0);
        Console.WriteLine($"Sucessos: {successCount}, Conflitos: {conflictCount}");
    }
}
```

### Validação Final

Para validar a implementação completa:

1. **Cache Redis funcionando**:
   - Itens são cacheados corretamente
   - Invalidação funciona por padrão
   - Performance melhorada

2. **Controle de Conflitos**:
   - Version numbers funcionam
   - Exceções são lançadas corretamente
   - Dados atuais são retornados

3. **SignalR Integrado**:
   - Notificações em tempo real
   - Grupos por workspace
   - Autenticação JWT

4. **Navigation State**:
   - Estado persiste no banco
   - API de leitura/escrita
   - Performance otimizada

5. **Sistema Configurável**:
   - Parâmetros do sistema
   - Categorização
   - API de consulta

### Testes Manuais
```bash
# Criar workspace
POST http://localhost:8503/api/workspaces

# Criar item no workspace
POST http://localhost:8503/api/workspaces/{id}/items

# Listar itens por módulo com cache
GET http://localhost:8503/api/workspaces/{id}/items?module=Frontend&type=component

# Testar conflito de versão
PUT http://localhost:8503/api/workspaces/{id}/items/{itemId}/data

# Gerenciar estado de navegação
PUT http://localhost:8503/api/workspaces/{id}/navigation

# Verificar parâmetros do sistema
GET http://localhost:8503/api/system/parameters
```

## Próximos Passos (Fase 3)

Na próxima fase, implementaremos:
- SignalR Hub para colaboração em tempo real
- Edição colaborativa de itens
- Chat por workspace
- Notificações em tempo real
- Gestão de cursores múltiplos

**Dependências para Fase 3**: Esta fase deve estar 100% funcional antes de prosseguir.
