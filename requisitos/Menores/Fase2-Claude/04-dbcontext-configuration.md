# Parte 4: DbContext Configuration - Workspace Core

## Contexto
Esta é a **Parte 4 de 12** da Fase 2 (Workspace Core). Aqui configuraremos o Entity Framework para todas as entidades criadas nas partes anteriores, com relacionamentos otimizados, índices e constraints.

**Pré-requisitos**: Partes 1, 2 e 3 (Todas as entidades devem estar criadas)

**Dependências**: ApplicationDbContext da Fase 1

**Próxima parte**: Parte 5 - Migrations e Seeds

## Objetivos desta Parte
✅ Configurar DbSets para todas as entidades do workspace  
✅ Definir relacionamentos e navigation properties  
✅ Criar índices para performance  
✅ Estabelecer constraints e validações  
✅ Configurar cascading deletes apropriadas  

## 1. Extensão do ApplicationDbContext

### 1.1 Novos DbSets

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Extensão)
```csharp
using IDE.Domain.Entities.System;
using IDE.Domain.Entities.Workspace;
using Microsoft.EntityFrameworkCore;

public partial class ApplicationDbContext : DbContext
{
    // DbSets existentes da Fase 1 (Users, Roles, etc.)
    // ... propriedades existentes ...

    // Novos DbSets da Fase 2 - Workspace Core
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<ModuleItem> ModuleItems { get; set; }
    public DbSet<WorkspacePhase> WorkspacePhases { get; set; }
    
    // Colaboração
    public DbSet<WorkspacePermission> WorkspacePermissions { get; set; }
    public DbSet<WorkspaceInvitation> WorkspaceInvitations { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    
    // Auxiliares
    public DbSet<ModuleItemTag> ModuleItemTags { get; set; }
    public DbSet<WorkspaceTag> WorkspaceTags { get; set; }
    public DbSet<WorkspaceVersion> WorkspaceVersions { get; set; }
    public DbSet<ItemVersion> ItemVersions { get; set; }
    public DbSet<WorkspaceNavigationState> WorkspaceNavigationStates { get; set; }
    
    // Sistema
    public DbSet<SystemParameter> SystemParameters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configurações existentes da Fase 1
        ConfigureIdentityEntities(modelBuilder);
        
        // Novas configurações da Fase 2
        ConfigureWorkspaceEntities(modelBuilder);
        ConfigureCollaborationEntities(modelBuilder);
        ConfigureAuxiliaryEntities(modelBuilder);
        ConfigureSystemEntities(modelBuilder);
        
        // Índices de performance
        ConfigurePerformanceIndexes(modelBuilder);
        
        // Seed data
        SeedWorkspaceData(modelBuilder);
    }
}
```

## 2. Configuração das Entidades Principais

### 2.1 Workspace Configuration

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Configurações)
```csharp
private void ConfigureWorkspaceEntities(ModelBuilder modelBuilder)
{
    // Configuração da entidade Workspace
    modelBuilder.Entity<Workspace>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        entity.Property(e => e.Description)
            .HasMaxLength(1000);
            
        entity.Property(e => e.SemanticVersion)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("1.0.0");
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.IsArchived)
            .HasDefaultValue(false);
        
        // Relacionamentos
        entity.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Restrict); // Não deletar usuário se tiver workspaces
            
        entity.HasOne(e => e.CurrentPhase)
            .WithMany()
            .HasForeignKey(e => e.CurrentPhaseId)
            .OnDelete(DeleteBehavior.SetNull); // Se fase for deletada, workspace fica sem fase atual
            
        // Constraints
        entity.HasIndex(e => new { e.OwnerId, e.Name })
            .HasDatabaseName("IX_Workspaces_Owner_Name")
            .IsUnique(); // Nome único por owner
            
        entity.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_Workspaces_CreatedAt");
            
        entity.HasIndex(e => e.IsArchived)
            .HasDatabaseName("IX_Workspaces_IsArchived");
    });
    
    // Configuração da entidade ModuleItem
    modelBuilder.Entity<ModuleItem>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(255);
            
        entity.Property(e => e.Content)
            .HasDefaultValue("");
            
        entity.Property(e => e.Module)
            .IsRequired()
            .HasMaxLength(100);
            
        entity.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(100);
            
        entity.Property(e => e.VersionNumber)
            .HasDefaultValue(1);
            
        entity.Property(e => e.Size)
            .HasDefaultValue(0);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Items)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade); // Se workspace for deletado, deleta items
            
        entity.HasOne(e => e.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Cascade); // Se parent for deletado, deleta children
            
        // Constraints e Índices
        entity.HasIndex(e => e.WorkspaceId)
            .HasDatabaseName("IX_ModuleItems_WorkspaceId");
            
        entity.HasIndex(e => e.ParentId)
            .HasDatabaseName("IX_ModuleItems_ParentId");
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Module })
            .HasDatabaseName("IX_ModuleItems_Workspace_Module");
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Type })
            .HasDatabaseName("IX_ModuleItems_Workspace_Type");
            
        entity.HasIndex(e => new { e.WorkspaceId, e.UpdatedAt })
            .HasDatabaseName("IX_ModuleItems_Workspace_UpdatedAt");
    });
    
    // Configuração da entidade WorkspacePhase
    modelBuilder.Entity<WorkspacePhase>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        entity.Property(e => e.Description)
            .HasMaxLength(500);
            
        entity.Property(e => e.Color)
            .HasMaxLength(20)
            .HasDefaultValue("#52c41a");
            
        entity.Property(e => e.IsDefault)
            .HasDefaultValue(false);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Phases)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Constraints
        entity.HasIndex(e => new { e.WorkspaceId, e.Name })
            .HasDatabaseName("IX_WorkspacePhases_Workspace_Name")
            .IsUnique(); // Nome único por workspace
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Order })
            .HasDatabaseName("IX_WorkspacePhases_Workspace_Order");
    });
}
```

## 3. Configuração das Entidades de Colaboração

### 3.1 Collaboration Entities Configuration

```csharp
private void ConfigureCollaborationEntities(ModelBuilder modelBuilder)
{
    // Configuração da entidade WorkspacePermission
    modelBuilder.Entity<WorkspacePermission>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Level)
            .IsRequired()
            .HasConversion<int>(); // Enum para int
            
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Permissions)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.GrantedBy)
            .WithMany()
            .HasForeignKey(e => e.GrantedById)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Constraints
        entity.HasIndex(e => new { e.WorkspaceId, e.UserId })
            .HasDatabaseName("IX_WorkspacePermissions_Workspace_User")
            .IsUnique(); // Um usuário, uma permissão por workspace
            
        entity.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_WorkspacePermissions_UserId");
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Level })
            .HasDatabaseName("IX_WorkspacePermissions_Workspace_Level");
    });
    
    // Configuração da entidade WorkspaceInvitation
    modelBuilder.Entity<WorkspaceInvitation>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(255);
            
        entity.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(255);
            
        entity.Property(e => e.Level)
            .IsRequired()
            .HasConversion<int>();
            
        entity.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(0); // Pending
            
        entity.Property(e => e.ExpiresAt)
            .IsRequired();
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.Message)
            .HasMaxLength(500);
            
        entity.Property(e => e.InvitedUserName)
            .HasMaxLength(255);
            
        entity.Property(e => e.IpAddress)
            .HasMaxLength(45);
            
        entity.Property(e => e.UserAgent)
            .HasMaxLength(500);
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Invitations)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.InvitedBy)
            .WithMany()
            .HasForeignKey(e => e.InvitedById)
            .OnDelete(DeleteBehavior.Restrict);
            
        entity.HasOne(e => e.AcceptedBy)
            .WithMany()
            .HasForeignKey(e => e.AcceptedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Constraints
        entity.HasIndex(e => e.Token)
            .HasDatabaseName("IX_WorkspaceInvitations_Token")
            .IsUnique(); // Token único
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Email, e.Status })
            .HasDatabaseName("IX_WorkspaceInvitations_Workspace_Email_Status");
            
        entity.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("IX_WorkspaceInvitations_ExpiresAt");
    });
    
    // Configuração da entidade ActivityLog
    modelBuilder.Entity<ActivityLog>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(100);
            
        entity.Property(e => e.Details)
            .HasMaxLength(2000);
            
        entity.Property(e => e.IpAddress)
            .HasMaxLength(45);
            
        entity.Property(e => e.UserAgent)
            .HasMaxLength(500);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.EntityType)
            .HasMaxLength(100);
            
        entity.Property(e => e.Category)
            .HasMaxLength(100);
            
        entity.Property(e => e.MetadataJson)
            .HasMaxLength(4000);
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Activities)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Índices para queries frequentes
        entity.HasIndex(e => new { e.WorkspaceId, e.CreatedAt })
            .HasDatabaseName("IX_ActivityLogs_Workspace_CreatedAt");
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Action })
            .HasDatabaseName("IX_ActivityLogs_Workspace_Action");
            
        entity.HasIndex(e => new { e.WorkspaceId, e.Category })
            .HasDatabaseName("IX_ActivityLogs_Workspace_Category");
            
        entity.HasIndex(e => e.EntityId)
            .HasDatabaseName("IX_ActivityLogs_EntityId");
            
        // Particionamento por data (opcional, para PostgreSQL)
        if (Database.IsNpgsql())
        {
            entity.ToTable("ActivityLogs", t => t.HasComment("Partitioned by CreatedAt month"));
        }
    });
}
```

## 4. Configuração das Entidades Auxiliares

### 4.1 Auxiliary Entities Configuration

```csharp
private void ConfigureAuxiliaryEntities(ModelBuilder modelBuilder)
{
    // Configuração da entidade ModuleItemTag
    modelBuilder.Entity<ModuleItemTag>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);
            
        entity.Property(e => e.Color)
            .HasMaxLength(20)
            .HasDefaultValue("#1890ff");
            
        entity.Property(e => e.Description)
            .HasMaxLength(200);
            
        entity.Property(e => e.Order)
            .HasDefaultValue(0);
            
        entity.Property(e => e.IsSystem)
            .HasDefaultValue(false);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
        
        // Relacionamentos
        entity.HasOne(e => e.ModuleItem)
            .WithMany(i => i.Tags)
            .HasForeignKey(e => e.ModuleItemId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Constraints
        entity.HasIndex(e => new { e.ModuleItemId, e.Name })
            .HasDatabaseName("IX_ModuleItemTags_Item_Name")
            .IsUnique(); // Nome único por item
    });
    
    // Configuração da entidade WorkspaceTag
    modelBuilder.Entity<WorkspaceTag>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);
            
        entity.Property(e => e.Color)
            .HasMaxLength(20)
            .HasDefaultValue("#1890ff");
            
        entity.Property(e => e.Description)
            .HasMaxLength(200);
            
        entity.Property(e => e.UsageCount)
            .HasDefaultValue(0);
            
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Tags)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Constraints
        entity.HasIndex(e => new { e.WorkspaceId, e.Name })
            .HasDatabaseName("IX_WorkspaceTags_Workspace_Name")
            .IsUnique();
    });
    
    // Configuração da entidade WorkspaceVersion
    modelBuilder.Entity<WorkspaceVersion>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Version)
            .IsRequired()
            .HasMaxLength(50);
            
        entity.Property(e => e.Description)
            .HasMaxLength(500);
            
        entity.Property(e => e.IsCurrent)
            .HasDefaultValue(false);
            
        entity.Property(e => e.IsSnapshot)
            .HasDefaultValue(false);
            
        entity.Property(e => e.ChangeType)
            .HasMaxLength(100);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.MetadataJson)
            .HasMaxLength(2000);
        
        // Relacionamentos
        entity.HasOne(e => e.Phase)
            .WithMany(p => p.Versions)
            .HasForeignKey(e => e.PhaseId)
            .OnDelete(DeleteBehavior.Restrict);
            
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.Versions)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Constraints
        entity.HasIndex(e => new { e.WorkspaceId, e.Version })
            .HasDatabaseName("IX_WorkspaceVersions_Workspace_Version")
            .IsUnique();
            
        entity.HasIndex(e => new { e.WorkspaceId, e.IsCurrent })
            .HasDatabaseName("IX_WorkspaceVersions_Workspace_IsCurrent");
    });
    
    // Configuração da entidade ItemVersion
    modelBuilder.Entity<ItemVersion>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Content)
            .IsRequired();
            
        entity.Property(e => e.Comment)
            .HasMaxLength(500);
            
        entity.Property(e => e.ChangeType)
            .HasMaxLength(100);
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.PreviousName)
            .HasMaxLength(255);
            
        entity.Property(e => e.PreviousModule)
            .HasMaxLength(100);
            
        entity.Property(e => e.PreviousType)
            .HasMaxLength(100);
            
        entity.Property(e => e.ContentHash)
            .HasMaxLength(64);
        
        // Relacionamentos
        entity.HasOne(e => e.Item)
            .WithMany(i => i.Versions)
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Índices
        entity.HasIndex(e => new { e.ItemId, e.VersionNumber })
            .HasDatabaseName("IX_ItemVersions_Item_VersionNumber")
            .IsUnique();
            
        entity.HasIndex(e => new { e.ItemId, e.CreatedAt })
            .HasDatabaseName("IX_ItemVersions_Item_CreatedAt");
    });
    
    // Configuração da entidade WorkspaceNavigationState
    modelBuilder.Entity<WorkspaceNavigationState>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.ModuleId)
            .IsRequired()
            .HasMaxLength(100);
            
        entity.Property(e => e.StateJson)
            .IsRequired();
            
        entity.Property(e => e.Version)
            .HasMaxLength(50)
            .HasDefaultValue("1.0");
            
        entity.Property(e => e.IsCompressed)
            .HasDefaultValue(false);
            
        entity.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.LastAccessedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.AccessCount)
            .HasDefaultValue(0);
        
        // Relacionamentos
        entity.HasOne(e => e.Workspace)
            .WithMany(w => w.NavigationStates)
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Constraints
        entity.HasIndex(e => new { e.WorkspaceId, e.UserId, e.ModuleId })
            .HasDatabaseName("IX_WorkspaceNavigationStates_Workspace_User_Module")
            .IsUnique(); // Um estado por usuário/workspace/módulo
    });
}
```

## 5. Configuração das Entidades de Sistema

### 5.1 System Entities Configuration

```csharp
private void ConfigureSystemEntities(ModelBuilder modelBuilder)
{
    // Configuração da entidade SystemParameter
    modelBuilder.Entity<SystemParameter>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(100);
            
        entity.Property(e => e.Value)
            .IsRequired()
            .HasMaxLength(1000);
            
        entity.Property(e => e.Description)
            .HasMaxLength(500);
            
        entity.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(50);
            
        entity.Property(e => e.DataType)
            .HasMaxLength(50)
            .HasDefaultValue("STRING");
            
        entity.Property(e => e.DefaultValue)
            .HasMaxLength(1000);
            
        entity.Property(e => e.IsReadOnly)
            .HasDefaultValue(false);
            
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
            
        entity.Property(e => e.ValidationPattern)
            .HasMaxLength(500);
            
        entity.Property(e => e.Environment)
            .HasMaxLength(200)
            .HasDefaultValue("ALL");
            
        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        entity.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
        
        // Relacionamentos
        entity.HasOne(e => e.UpdatedBy)
            .WithMany()
            .HasForeignKey(e => e.UpdatedById)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Constraints
        entity.HasIndex(e => e.Key)
            .HasDatabaseName("IX_SystemParameters_Key")
            .IsUnique(); // Chave única
            
        entity.HasIndex(e => e.Category)
            .HasDatabaseName("IX_SystemParameters_Category");
            
        entity.HasIndex(e => new { e.Category, e.IsActive })
            .HasDatabaseName("IX_SystemParameters_Category_IsActive");
    });
}
```

## 6. Índices de Performance

### 6.1 Performance Indexes

```csharp
private void ConfigurePerformanceIndexes(ModelBuilder modelBuilder)
{
    // Índices compostos para queries frequentes
    
    // Workspace listing por usuário (permissions + workspace info)
    modelBuilder.Entity<WorkspacePermission>()
        .HasIndex(e => new { e.UserId, e.IsActive })
        .HasDatabaseName("IX_WorkspacePermissions_User_Active")
        .HasFilter("IsActive = true");
    
    // Items por workspace/módulo (view principal)
    modelBuilder.Entity<ModuleItem>()
        .HasIndex(e => new { e.WorkspaceId, e.Module, e.ParentId })
        .HasDatabaseName("IX_ModuleItems_Workspace_Module_Parent")
        .HasFilter("ParentId IS NULL"); // Apenas roots
    
    // Activity logs recentes por workspace
    modelBuilder.Entity<ActivityLog>()
        .HasIndex(e => new { e.WorkspaceId, e.CreatedAt })
        .HasDatabaseName("IX_ActivityLogs_Workspace_CreatedAt_Desc")
        .IsDescending(false, true); // CreatedAt descendente
    
    // Navigation states por usuário ativo
    modelBuilder.Entity<WorkspaceNavigationState>()
        .HasIndex(e => new { e.UserId, e.LastAccessedAt })
        .HasDatabaseName("IX_WorkspaceNavigationStates_User_LastAccessed")
        .IsDescending(false, true);
    
    // Tags mais usadas por workspace
    modelBuilder.Entity<WorkspaceTag>()
        .HasIndex(e => new { e.WorkspaceId, e.UsageCount, e.IsActive })
        .HasDatabaseName("IX_WorkspaceTags_Workspace_Usage_Active")
        .HasFilter("IsActive = true")
        .IsDescending(false, true, false);
    
    // Convites pendentes (limpeza automática)
    modelBuilder.Entity<WorkspaceInvitation>()
        .HasIndex(e => new { e.Status, e.ExpiresAt })
        .HasDatabaseName("IX_WorkspaceInvitations_Status_ExpiresAt")
        .HasFilter("Status = 0"); // Apenas pendentes
    
    // Versões atuais por workspace
    modelBuilder.Entity<WorkspaceVersion>()
        .HasIndex(e => new { e.WorkspaceId, e.IsCurrent })
        .HasDatabaseName("IX_WorkspaceVersions_Workspace_IsCurrent")
        .HasFilter("IsCurrent = true");
    
    // Item versions por item (histórico)
    modelBuilder.Entity<ItemVersion>()
        .HasIndex(e => new { e.ItemId, e.VersionNumber })
        .HasDatabaseName("IX_ItemVersions_Item_Version_Desc")
        .IsDescending(false, true);
}
```

## 7. Configurações de Cascata e Constraints

### 7.1 Delete Behavior Rules

```csharp
/*
CASCADING DELETES CONFIGURADAS:

1. Workspace -> ModuleItem (CASCADE)
   - Deletar workspace deleta todos os items

2. ModuleItem -> ModuleItem (CASCADE) 
   - Deletar item pai deleta filhos

3. Workspace -> WorkspacePermission (CASCADE)
   - Deletar workspace remove todas as permissões

4. Workspace -> ActivityLog (CASCADE)
   - Deletar workspace remove logs (conforme retenção)

5. ModuleItem -> ItemVersion (CASCADE)
   - Deletar item remove histórico de versões

RESTRICT BEHAVIORS:

1. User -> Workspace (RESTRICT)
   - Não pode deletar usuário que possui workspaces

2. User -> ActivityLog (RESTRICT)
   - Preservar logs mesmo se usuário for removido

SET NULL BEHAVIORS:

1. WorkspacePhase -> Workspace.CurrentPhaseId (SET NULL)
   - Se fase atual for deletada, workspace fica sem fase

2. User -> SystemParameter.UpdatedById (SET NULL)
   - Preservar parâmetro mesmo se usuário for removido
*/
```

## 8. Características Implementadas

✅ **DbSets configurados** para todas as 12 entidades  
✅ **Relacionamentos otimizados** com cascade adequadas  
✅ **Índices de performance** para queries frequentes  
✅ **Constraints únicos** para integridade de dados  
✅ **Defaults apropriados** para campos obrigatórios  
✅ **Filtros em índices** para melhor performance  
✅ **Configurações de tamanho** otimizadas  
✅ **Suporte a PostgreSQL** específico onde necessário  

## 9. Próximos Passos

**Parte 5**: Migrations e Seeds
- Migration inicial da Fase 2
- Seed data para SystemParameters
- Scripts de limpeza e manutenção

**Validação desta Parte**:
- [ ] DbContext compila sem erros
- [ ] Todas as entidades estão mapeadas
- [ ] Relacionamentos funcionam corretamente
- [ ] Índices estão criados
- [ ] Constraints funcionam apropriadamente

## 10. Notas Importantes

⚠️ **Migration será grande** - considerar dividir em partes se necessário  
⚠️ **Índices de performance** podem impactar na escrita - monitorar  
⚠️ **Activity logs** crescerão rapidamente - configurar particionamento  
⚠️ **Navigation states** podem ter JSONs grandes - considerar compressão  
⚠️ **Cascading deletes** são irreversíveis - testar bem  
⚠️ **SystemParameters** devem ser cacheados para performance  

Esta parte estabelece a **configuração completa do banco de dados** para a Fase 2. A próxima parte criará as migrations e seeds necessários para inicializar o sistema.