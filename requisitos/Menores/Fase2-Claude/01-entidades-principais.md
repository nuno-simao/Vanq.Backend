# Parte 1: Entidades Principais - Workspace Core

## Contexto
Esta é a **Parte 1 de 12** da Fase 2 (Workspace Core). Aqui implementaremos as entidades fundamentais que formam a base do sistema de workspace.

**Pré-requisitos**: Fase 1 (Fundação e Autenticação) deve estar 100% funcional

**Dependências**: Nenhuma (é a base)

**Próxima parte**: Parte 2 - Entidades de Colaboração

## Objetivos desta Parte
✅ Criar entidade `Workspace` principal  
✅ Criar entidade `ModuleItem` com hierarquia ilimitada  
✅ Criar entidade `WorkspacePhase` para organização  
✅ Definir enums fundamentais  
✅ Estabelecer estrutura base para próximas partes  

## 1. Entidades Principais

### 1.1 Workspace.cs

#### IDE.Domain/Entities/Workspace/Workspace.cs
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Workspace
{
    public class Workspace
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }
        
        [MaxLength(1000)]
        public string Description { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string SemanticVersion { get; set; } = "1.0.0";
        
        public Guid? CurrentPhaseId { get; set; }
        public WorkspacePhase CurrentPhase { get; set; }
        
        public bool IsArchived { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public Guid OwnerId { get; set; }
        public User Owner { get; set; }
        
        // Navigation Properties (serão definidas nas próximas partes)
        public List<ModuleItem> Items { get; set; } = new();
        public List<WorkspacePermission> Permissions { get; set; } = new();
        public List<WorkspaceTag> Tags { get; set; } = new();
        public List<WorkspaceInvitation> Invitations { get; set; } = new();
        public List<ActivityLog> Activities { get; set; } = new();
        public List<WorkspaceVersion> Versions { get; set; } = new();
        public List<WorkspacePhase> Phases { get; set; } = new();
        public List<WorkspaceNavigationState> NavigationStates { get; set; } = new();
    }
}
```

### 1.2 ModuleItem.cs (ATUALIZADO)

#### IDE.Domain/Entities/Workspace/ModuleItem.cs
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Workspace
{
    public class ModuleItem
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; }
        
        public string Content { get; set; } = "";
        
        [Required]
        [MaxLength(100)]
        public string Module { get; set; }          // Ex: "Documents", "Tests", "Environments", "API"
        
        [Required]
        [MaxLength(100)]
        public string Type { get; set; }            // Ex: "typescript", "markdown", "uml-diagram", "database-table"
        
        // Hierarquia ilimitada
        public Guid? ParentId { get; set; }         
        public ModuleItem Parent { get; set; }
        public List<ModuleItem> Children { get; set; } = new();
        
        // Controle de conflitos
        public int VersionNumber { get; set; } = 1; 
        
        public long Size { get; set; }              // Tamanho do conteúdo em bytes
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        // Navigation Properties (serão definidas nas próximas partes)
        public List<ItemVersion> Versions { get; set; } = new();
        public List<ModuleItemTag> Tags { get; set; } = new();
    }
}
```

### 1.3 WorkspacePhase.cs

#### IDE.Domain/Entities/Workspace/WorkspacePhase.cs
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IDE.Domain.Entities.Workspace
{
    public class WorkspacePhase
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }
        
        [MaxLength(500)]
        public string Description { get; set; }
        
        [MaxLength(20)]
        public string Color { get; set; } = "#52c41a"; // Cor padrão verde
        
        public int Order { get; set; }
        
        public bool IsDefault { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        // Navigation Properties (serão definidas nas próximas partes)
        public List<WorkspaceVersion> Versions { get; set; } = new();
    }
}
```

## 2. Enums Fundamentais

### 2.1 PermissionLevel.cs

#### IDE.Domain/Enums/PermissionLevel.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum PermissionLevel
    {
        Owner = 0,    // Proprietário - acesso total
        Editor = 1,   // Editor - pode criar/editar/deletar items
        Reader = 2    // Leitor - apenas visualização
    }
}
```

### 2.2 InvitationStatus.cs

#### IDE.Domain/Enums/InvitationStatus.cs
```csharp
namespace IDE.Domain.Enums
{
    public enum InvitationStatus
    {
        Pending = 0,   // Convite enviado, aguardando resposta
        Accepted = 1,  // Convite aceito
        Rejected = 2,  // Convite rejeitado
        Expired = 3    // Convite expirado
    }
}
```

## 3. Conceito de Workspace na IDE

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

### Módulos Padrão Suportados
- **"Documents"**: Documentação, especificações, manuais
- **"Tests"**: Testes unitários, integração, e2e
- **"Environments"**: Configurações de ambiente, deploy
- **"API"**: Código de backend, services, controllers

### Tipos de Item Suportados
- **"typescript"**: Arquivos TypeScript
- **"javascript"**: Arquivos JavaScript  
- **"json"**: Arquivos de configuração JSON
- **"markdown"**: Documentação em Markdown
- **"html"**: Templates HTML
- **"css"**: Estilos CSS
- **"sql"**: Scripts SQL
- **"text"**: Arquivos de texto simples
- **"uml-diagram"**: Diagramas UML
- **"database"**: Definições de banco de dados
- **"database-table"**: Tabelas específicas
- **"config"**: Arquivos de configuração diversos

## 4. Sistema de Planos (Preparação)

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

## 5. Características Implementadas

✅ **Entidade Workspace** com versionamento semântico  
✅ **Entidade ModuleItem** com hierarquia ilimitada via ParentId  
✅ **Field Module** como string livre para categorização  
✅ **Field Type** como string para definir tipo do item  
✅ **WorkspacePhase** para organização customizável  
✅ **Controle de versão** via VersionNumber para conflitos  
✅ **Enums fundamentais** para permissões e convites  
✅ **Navigation properties** preparadas para próximas partes  

## 6. Próximos Passos

**Parte 2**: Entidades de Colaboração
- WorkspacePermission
- WorkspaceInvitation  
- ActivityLog

**Validação desta Parte**:
- [ ] Entidades compilam sem erros
- [ ] Relationships estão definidos
- [ ] Enums estão acessíveis
- [ ] Navigation properties preparadas

## 7. Notas Importantes

⚠️ **As navigation properties** (Lists) estão definidas mas serão populadas nas próximas partes  
⚠️ **Entity Framework Configuration** será feita na Parte 4  
⚠️ **Migration** será criada na Parte 5  
⚠️ **User entity** deve existir da Fase 1 (autenticação)  

Esta parte estabelece a **fundação sólida** para todo o sistema de workspace. As próximas partes irão complementar com colaboração, cache, services e APIs.