# Parte 3: Entidades Auxiliares - Workspace Core

## Contexto
Esta é a **Parte 3 de 12** da Fase 2 (Workspace Core). Aqui implementaremos as entidades auxiliares que complementam o sistema com tags, versionamento, navegação e configurações.

**Pré-requisitos**: Parte 1 (Entidades Principais) e Parte 2 (Entidades de Colaboração) devem estar concluídas

**Dependências**: Workspace, ModuleItem, User entities

**Próxima parte**: Parte 4 - DbContext Configuration

## Objetivos desta Parte
✅ Criar entidades `ModuleItemTag` e `WorkspaceTag` para organização  
✅ Criar entidades `WorkspaceVersion` e `ItemVersion` para versionamento  
✅ Criar entidade `WorkspaceNavigationState` para persistência de estado  
✅ Criar entidade `SystemParameter` para configurações  
✅ Completar o modelo de dados da Fase 2  

## 1. Entidades de Tags

### 1.1 ModuleItemTag.cs

#### IDE.Domain/Entities/Workspace/ModuleItemTag.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Workspace
{
    public class ModuleItemTag
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }
        
        [MaxLength(20)]
        public string Color { get; set; } = "#1890ff";   // Cor padrão Ant Design (azul)
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Key
        [Required]
        public Guid ModuleItemId { get; set; }
        public ModuleItem ModuleItem { get; set; }
        
        // Campos adicionais
        [MaxLength(200)]
        public string Description { get; set; }           // Descrição opcional da tag
        
        public int Order { get; set; } = 0;              // Ordem de exibição
        
        public bool IsSystem { get; set; } = false;      // Tags do sistema vs usuário
        
        // Metadados
        public Guid? CreatedById { get; set; }           // Usuário que criou a tag
        public User CreatedBy { get; set; }
    }
}
```

### 1.2 WorkspaceTag.cs

#### IDE.Domain/Entities/Workspace/WorkspaceTag.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Workspace
{
    public class WorkspaceTag
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }
        
        [MaxLength(20)]
        public string Color { get; set; } = "#1890ff";   // Cor padrão Ant Design (azul)
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Key
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        // Campos adicionais
        [MaxLength(200)]
        public string Description { get; set; }           // Descrição da tag
        
        public int UsageCount { get; set; } = 0;         // Quantos items usam esta tag
        
        public bool IsActive { get; set; } = true;       // Para desativar sem deletar
        
        // Metadados
        public Guid? CreatedById { get; set; }           // Usuário que criou a tag
        public User CreatedBy { get; set; }
    }
}
```

## 2. Entidades de Versionamento

### 2.1 WorkspaceVersion.cs

#### IDE.Domain/Entities/Workspace/WorkspaceVersion.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Identity;

namespace IDE.Domain.Entities.Workspace
{
    public class WorkspaceVersion
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Version { get; set; }               // Ex: "1.0.0", "1.1.0", "2.0.0"
        
        [MaxLength(500)]
        public string Description { get; set; }           // Descrição das mudanças
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        [Required]
        public Guid PhaseId { get; set; }
        public WorkspacePhase Phase { get; set; }         // Fase em que a versão foi criada
        
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        [Required]
        public Guid CreatedById { get; set; }
        public User CreatedBy { get; set; }
        
        // Campos adicionais
        public bool IsCurrent { get; set; } = false;     // Se é a versão atual
        
        public bool IsSnapshot { get; set; } = false;    // Se é snapshot automático vs manual
        
        [MaxLength(100)]
        public string ChangeType { get; set; }           // "MAJOR", "MINOR", "PATCH", "SNAPSHOT"
        
        public int TotalItems { get; set; }              // Número de items nesta versão
        
        public long TotalSize { get; set; }              // Tamanho total em bytes
        
        // Metadados JSON
        [MaxLength(2000)]
        public string MetadataJson { get; set; }          // Dados adicionais da versão
    }
}
```

### 2.2 ItemVersion.cs

#### IDE.Domain/Entities/Workspace/ItemVersion.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Identity;

namespace IDE.Domain.Entities.Workspace
{
    public class ItemVersion
    {
        public Guid Id { get; set; }
        
        [Required]
        public string Content { get; set; }              // Conteúdo do item nesta versão
        
        [MaxLength(500)]
        public string Comment { get; set; }              // Comentário da mudança
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        [Required]
        public Guid ItemId { get; set; }
        public ModuleItem Item { get; set; }
        
        [Required]
        public Guid CreatedById { get; set; }
        public User CreatedBy { get; set; }
        
        // Campos de controle
        public int VersionNumber { get; set; }           // Número sequencial da versão do item
        
        public long Size { get; set; }                   // Tamanho do conteúdo em bytes
        
        [MaxLength(100)]
        public string ChangeType { get; set; }           // "CREATE", "UPDATE", "DELETE", "RESTORE"
        
        // Campos para comparação
        public int LinesAdded { get; set; } = 0;
        public int LinesRemoved { get; set; } = 0;
        public int LinesModified { get; set; } = 0;
        
        // Metadados
        [MaxLength(255)]
        public string PreviousName { get; set; }         // Nome anterior (se mudou)
        
        [MaxLength(100)]
        public string PreviousModule { get; set; }       // Módulo anterior (se mudou)
        
        [MaxLength(100)]
        public string PreviousType { get; set; }         // Tipo anterior (se mudou)
        
        // Hash do conteúdo para detecção de mudanças
        [MaxLength(64)]
        public string ContentHash { get; set; }          // SHA256 do conteúdo
    }
}
```

## 3. Entidade de Estado de Navegação

### 3.1 WorkspaceNavigationState.cs

#### IDE.Domain/Entities/Workspace/WorkspaceNavigationState.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Workspace
{
    public class WorkspaceNavigationState
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ModuleId { get; set; }             // ID do módulo no frontend (ex: "Documents", "API")
        
        [Required]
        public string StateJson { get; set; }            // Estado serializado do módulo
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        [Required]
        public Guid UserId { get; set; }                 // Estado é por usuário
        public User User { get; set; }
        
        // Campos adicionais
        [MaxLength(50)]
        public string Version { get; set; } = "1.0";     // Versão do formato do estado
        
        public bool IsCompressed { get; set; } = false;  // Se o JSON está comprimido
        
        public long Size { get; set; }                   // Tamanho do estado em bytes
        
        // Metadados para performance
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        
        public int AccessCount { get; set; } = 0;        // Quantas vezes foi acessado
    }
}
```

## 4. Entidade de Parâmetros do Sistema

### 4.1 SystemParameter.cs

#### IDE.Domain/Entities/System/SystemParameter.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.System
{
    public class SystemParameter
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Key { get; set; }                  // Ex: "MAX_WORKSPACES_FREE", "LOG_RETENTION_PRO"
        
        [Required]
        [MaxLength(1000)]
        public string Value { get; set; }                // Valor do parâmetro
        
        [MaxLength(500)]
        public string Description { get; set; }          // Descrição do parâmetro
        
        [Required]
        [MaxLength(50)]
        public string Category { get; set; }             // Ex: "QUOTAS", "RETENTION", "FEATURES", "CACHE"
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Campos adicionais
        [MaxLength(50)]
        public string DataType { get; set; } = "STRING"; // "STRING", "INTEGER", "BOOLEAN", "JSON"
        
        [MaxLength(1000)]
        public string DefaultValue { get; set; }         // Valor padrão
        
        public bool IsReadOnly { get; set; } = false;    // Parâmetros que não podem ser alterados
        
        public bool IsActive { get; set; } = true;       // Para desativar parâmetros
        
        // Validações
        [MaxLength(500)]
        public string ValidationPattern { get; set; }    // Regex para validação
        
        public int? MinValue { get; set; }               // Valor mínimo (para números)
        public int? MaxValue { get; set; }               // Valor máximo (para números)
        
        // Metadados
        public Guid? UpdatedById { get; set; }           // Usuário que atualizou
        public User UpdatedBy { get; set; }
        
        [MaxLength(200)]
        public string Environment { get; set; }          // "DEVELOPMENT", "PRODUCTION", "ALL"
    }
}
```

## 5. Parâmetros do Sistema Pré-definidos

### 5.1 Categorias de Parâmetros

#### QUOTAS - Limites por Plano
```csharp
// Implementação será na Parte 5 (Seeds)
"MAX_WORKSPACES_FREE" = "1"
"MAX_WORKSPACES_PRO" = "5"
"MAX_WORKSPACES_ENTERPRISE" = "15"
"MAX_USERS_PER_WORKSPACE_FREE" = "2"
"MAX_USERS_PER_WORKSPACE_PRO" = "10"
"MAX_USERS_PER_WORKSPACE_ENTERPRISE" = "25"
"MAX_ITEMS_PER_WORKSPACE_FREE" = "100"
"MAX_ITEMS_PER_WORKSPACE_PRO" = "1000"
"MAX_ITEMS_PER_WORKSPACE_ENTERPRISE" = "5000"
```

#### RETENTION - Retenção de Dados
```csharp
"LOG_RETENTION_FREE_DAYS" = "7"
"LOG_RETENTION_PRO_DAYS" = "30"
"LOG_RETENTION_ENTERPRISE_DAYS" = "90"
"CACHE_RETENTION_HOURS" = "24"
"INVITATION_EXPIRY_DAYS" = "7"
```

#### FEATURES - Funcionalidades
```csharp
"ENABLE_REAL_TIME_SYNC" = "true"
"ENABLE_VERSION_HISTORY" = "true"
"ENABLE_ANALYTICS_PRO" = "false"
"ENABLE_ANALYTICS_ENTERPRISE" = "true"
"MAX_FILE_SIZE_MB" = "10"
```

#### CACHE - Configurações de Cache
```csharp
"CACHE_WORKSPACE_TTL_MINUTES" = "30"
"CACHE_ITEMS_TTL_MINUTES" = "15"
"CACHE_NAVIGATION_TTL_MINUTES" = "60"
"REDIS_KEY_PREFIX" = "ide_workspace"
```

## 6. Estruturas de Estado de Navegação

### 6.1 Exemplo de StateJson

```json
{
  "moduleId": "Documents",
  "expandedNodes": [
    "root",
    "folder-api-docs", 
    "folder-user-guides"
  ],
  "selectedItems": [
    "item-authentication-md",
    "item-rate-limiting-md"
  ],
  "activeTab": "editor",
  "scrollPosition": {
    "x": 0,
    "y": 1240
  },
  "editorState": {
    "cursorPosition": {
      "line": 45,
      "column": 12
    },
    "selection": {
      "start": { "line": 45, "column": 12 },
      "end": { "line": 45, "column": 25 }
    }
  },
  "sidebarWidth": 280,
  "panelSizes": {
    "tree": 0.3,
    "editor": 0.7
  },
  "filters": {
    "type": "all",
    "tags": [],
    "module": "Documents"
  },
  "viewMode": "tree",
  "timestamp": "2025-09-24T10:30:00Z"
}
```

### 6.2 Compressão de Estado

Para estados grandes, implementaremos compressão:

```csharp
// Será implementado nos services
// Estados > 5KB serão comprimidos com gzip
// Flag IsCompressed = true
// Descompressão automática na leitura
```

## 7. Versionamento Semântico

### 7.1 Formato de Versões

- **MAJOR.MINOR.PATCH** (ex: 2.1.3)
- **MAJOR**: Mudanças incompatíveis
- **MINOR**: Funcionalidades compatíveis
- **PATCH**: Correções compatíveis

### 7.2 Tipos de Versão

- **Manual**: Criada pelo usuário
- **Snapshot**: Criada automaticamente pelo sistema
- **Milestone**: Marcos importantes do projeto
- **Release**: Versões de produção

## 8. Características Implementadas

✅ **Sistema de tags** para workspace e items com cores  
✅ **Versionamento completo** com histórico detalhado  
✅ **Estado de navegação** persistente por usuário/workspace  
✅ **Parâmetros configuráveis** por categoria  
✅ **Controle de quotas** por plano de usuário  
✅ **Metadados ricos** em todas as entidades  
✅ **Compressão de estado** para performance  
✅ **Validação de parâmetros** com patterns  

## 9. Próximos Passos

**Parte 4**: DbContext Configuration
- Entity Framework configurations
- Relationships e constraints
- Indexes para performance
- Configurações específicas por entidade

**Validação desta Parte**:
- [ ] Entidades auxiliares compilam sem erros
- [ ] Navigation properties estão definidas
- [ ] Constraints e validações estão corretas
- [ ] Relacionamentos com outras entidades estão definidos
- [ ] Parâmetros do sistema estão categorizados

## 10. Notas Importantes

⚠️ **StateJson pode ser grande** - considerar compressão  
⚠️ **SystemParameter updates** devem ser auditados  
⚠️ **Tags duplicadas** devem ser evitadas por workspace  
⚠️ **Version cleanup** será necessário para não acumular muito histórico  
⚠️ **Navigation state** deve ter TTL no cache  
⚠️ **ContentHash** deve ser calculado automaticamente  

Esta parte completa o **modelo de dados** da Fase 2. A próxima parte irá configurar o Entity Framework para todas essas entidades com relacionamentos otimizados e indexes de performance.