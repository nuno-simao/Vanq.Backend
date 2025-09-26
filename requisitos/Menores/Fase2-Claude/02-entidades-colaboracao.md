# Parte 2: Entidades de Colaboração - Workspace Core

## Contexto
Esta é a **Parte 2 de 12** da Fase 2 (Workspace Core). Aqui implementaremos as entidades que permitem colaboração entre usuários nos workspaces.

**Pré-requisitos**: Parte 1 (Entidades Principais) deve estar concluída

**Dependências**: Workspace, User entities (Fase 1)

**Próxima parte**: Parte 3 - Entidades Auxiliares

## Objetivos desta Parte
✅ Criar entidade `WorkspacePermission` para controle de acesso  
✅ Criar entidade `WorkspaceInvitation` para sistema de convites  
✅ Criar entidade `ActivityLog` para auditoria  
✅ Implementar sistema de colaboração completo  
✅ Estabelecer base para controle de permissões  

## 1. Entidades de Colaboração

### 1.1 WorkspacePermission.cs

#### IDE.Domain/Entities/Workspace/WorkspacePermission.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Identity;
using IDE.Domain.Enums;

namespace IDE.Domain.Entities.Workspace
{
    public class WorkspacePermission
    {
        public Guid Id { get; set; }
        
        [Required]
        public PermissionLevel Level { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Campos adicionais para controle
        public DateTime? ExpiresAt { get; set; }        // Permissão temporária (opcional)
        public bool IsActive { get; set; } = true;      // Para desativar sem deletar
        public Guid? GrantedById { get; set; }          // Quem concedeu a permissão
        public User GrantedBy { get; set; }
    }
}
```

### 1.2 WorkspaceInvitation.cs

#### IDE.Domain/Entities/Workspace/WorkspaceInvitation.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Identity;
using IDE.Domain.Enums;

namespace IDE.Domain.Entities.Workspace
{
    public class WorkspaceInvitation
    {
        public Guid Id { get; set; }
        
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Token { get; set; }               // Token único para aceitar convite
        
        [Required]
        public PermissionLevel Level { get; set; }
        
        [Required]
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
        
        [Required]
        public DateTime ExpiresAt { get; set; }         // Data de expiração do convite
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        
        // Foreign Keys
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        [Required]
        public Guid InvitedById { get; set; }           // Usuário que fez o convite
        public User InvitedBy { get; set; }
        
        public Guid? AcceptedByUserId { get; set; }     // Usuário que aceitou (pode ser diferente se email não corresponder)
        public User AcceptedBy { get; set; }
        
        // Campos adicionais
        [MaxLength(500)]
        public string Message { get; set; }             // Mensagem personalizada do convite
        
        [MaxLength(255)]
        public string InvitedUserName { get; set; }     // Nome sugerido para o usuário convidado
        
        // Metadados
        [MaxLength(45)]
        public string IpAddress { get; set; }           // IP de onde veio o convite
        
        [MaxLength(500)]
        public string UserAgent { get; set; }           // User agent de onde veio o convite
    }
}
```

### 1.3 ActivityLog.cs

#### IDE.Domain/Entities/Workspace/ActivityLog.cs
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using IDE.Domain.Entities.Identity;

namespace IDE.Domain.Entities.Workspace
{
    public class ActivityLog
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Action { get; set; }              // Ex: "ITEM_CREATED", "WORKSPACE_UPDATED", "USER_INVITED"
        
        [MaxLength(2000)]
        public string Details { get; set; }             // JSON ou texto com detalhes da ação
        
        [MaxLength(45)]
        public string IpAddress { get; set; }
        
        [MaxLength(500)]
        public string UserAgent { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        [Required]
        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Campos adicionais para contexto
        public Guid? EntityId { get; set; }             // ID da entidade afetada (Item, Permission, etc.)
        
        [MaxLength(100)]
        public string EntityType { get; set; }          // Tipo da entidade ("ModuleItem", "WorkspacePermission", etc.)
        
        [MaxLength(100)]
        public string Category { get; set; }            // Categoria da ação ("SECURITY", "CONTENT", "COLLABORATION")
        
        public int? OldVersionNumber { get; set; }      // Versão anterior (para items)
        public int? NewVersionNumber { get; set; }      // Nova versão (para items)
        
        // Metadados JSON serializados
        [MaxLength(4000)]
        public string MetadataJson { get; set; }        // Dados adicionais em JSON
    }
}
```

## 2. Enums Adicionais para Colaboração

### 2.1 ActivityCategory.cs

#### IDE.Domain/Enums/ActivityCategory.cs
```csharp
namespace IDE.Domain.Enums
{
    public static class ActivityCategory
    {
        public const string Security = "SECURITY";           // Mudanças de permissão, convites
        public const string Content = "CONTENT";             // Criação/edição de items
        public const string Collaboration = "COLLABORATION"; // Ações colaborativas
        public const string Workspace = "WORKSPACE";         // Mudanças no workspace
        public const string System = "SYSTEM";               // Ações do sistema
    }
}
```

### 2.2 ActivityAction.cs

#### IDE.Domain/Enums/ActivityAction.cs
```csharp
namespace IDE.Domain.Enums
{
    public static class ActivityAction
    {
        // Workspace Actions
        public const string WorkspaceCreated = "WORKSPACE_CREATED";
        public const string WorkspaceUpdated = "WORKSPACE_UPDATED";
        public const string WorkspaceDeleted = "WORKSPACE_DELETED";
        public const string WorkspaceArchived = "WORKSPACE_ARCHIVED";
        public const string WorkspaceRestored = "WORKSPACE_RESTORED";
        
        // Item Actions
        public const string ItemCreated = "ITEM_CREATED";
        public const string ItemUpdated = "ITEM_UPDATED";
        public const string ItemDeleted = "ITEM_DELETED";
        public const string ItemMoved = "ITEM_MOVED";
        public const string ItemRenamed = "ITEM_RENAMED";
        public const string ItemSaved = "ITEM_SAVED";           // Versionamento manual
        
        // Collaboration Actions
        public const string UserInvited = "USER_INVITED";
        public const string InvitationAccepted = "INVITATION_ACCEPTED";
        public const string InvitationRejected = "INVITATION_REJECTED";
        public const string PermissionGranted = "PERMISSION_GRANTED";
        public const string PermissionRevoked = "PERMISSION_REVOKED";
        public const string PermissionUpdated = "PERMISSION_UPDATED";
        
        // Phase Actions
        public const string PhaseCreated = "PHASE_CREATED";
        public const string PhaseUpdated = "PHASE_UPDATED";
        public const string PhaseDeleted = "PHASE_DELETED";
        public const string WorkspacePromoted = "WORKSPACE_PROMOTED";
        
        // Tag Actions
        public const string TagCreated = "TAG_CREATED";
        public const string TagDeleted = "TAG_DELETED";
        public const string TagAssigned = "TAG_ASSIGNED";
        public const string TagRemoved = "TAG_REMOVED";
        
        // Version Actions
        public const string VersionCreated = "VERSION_CREATED";
        public const string VersionRestored = "VERSION_RESTORED";
        
        // System Actions
        public const string UserJoined = "USER_JOINED";
        public const string UserLeft = "USER_LEFT";
        public const string CacheCleared = "CACHE_CLEARED";
    }
}
```

## 3. Sistema de Permissões

### 3.1 Níveis de Permissão

```csharp
// Já definido na Parte 1
public enum PermissionLevel
{
    Owner = 0,    // Proprietário - acesso total, pode deletar workspace
    Editor = 1,   // Editor - pode criar/editar/deletar items, convidar usuários
    Reader = 2    // Leitor - apenas visualização, não pode modificar
}
```

### 3.2 Matrix de Permissões

| Ação | Owner | Editor | Reader |
|------|-------|--------|--------|
| Ver workspace | ✅ | ✅ | ✅ |
| Ver items | ✅ | ✅ | ✅ |
| Criar items | ✅ | ✅ | ❌ |
| Editar items | ✅ | ✅ | ❌ |
| Deletar items | ✅ | ✅ | ❌ |
| Mover items | ✅ | ✅ | ❌ |
| Criar tags | ✅ | ✅ | ❌ |
| Convidar usuários | ✅ | ✅ | ❌ |
| Gerenciar permissões | ✅ | ❌ | ❌ |
| Editar workspace | ✅ | ❌ | ❌ |
| Deletar workspace | ✅ | ❌ | ❌ |
| Ver activity logs | ✅ | ✅ | ✅ |
| Gerenciar fases | ✅ | ❌ | ❌ |
| Criar versões | ✅ | ✅ | ❌ |

## 4. Sistema de Convites

### 4.1 Fluxo de Convite

1. **Criação do Convite**:
   - Usuário Owner/Editor convida por email
   - Token único gerado
   - Prazo de expiração definido (7 dias padrão)
   - Email de notificação enviado

2. **Aceitação do Convite**:
   - Usuário clica no link com token
   - Se não tem conta, é redirecionado para registro
   - Se tem conta, aceita automaticamente
   - Permissão é criada automaticamente

3. **Estados do Convite**:
   - **Pending**: Aguardando resposta
   - **Accepted**: Aceito e permissão criada
   - **Rejected**: Rejeitado pelo usuário
   - **Expired**: Expirou o prazo

### 4.2 Validação de Convites

```csharp
// Exemplos de validação que serão implementados nos services

// Token deve ser único
// Email deve ser válido
// Workspace deve existir
// Usuário que convida deve ter permissão Editor+
// Não pode existir convite pendente para mesmo email/workspace
// Data de expiração deve ser no futuro
```

## 5. Sistema de Activity Log

### 5.1 Categorias de Logs

- **SECURITY**: Mudanças de permissão, convites, acessos
- **CONTENT**: Criação, edição, deletion de items
- **COLLABORATION**: Ações colaborativas, chat, comentários
- **WORKSPACE**: Mudanças nas configurações do workspace
- **SYSTEM**: Ações automáticas do sistema

### 5.2 Retenção de Logs por Plano

- **Free**: 7 dias de retenção
- **Pro**: 30 dias de retenção  
- **Enterprise**: 90 dias de retenção

### 5.3 Estrutura de Detalhes JSON

```json
{
  "oldValue": "Nome anterior",
  "newValue": "Novo nome",
  "itemType": "typescript",
  "module": "API",
  "affectedUsers": ["user1@email.com", "user2@email.com"],
  "changeReason": "Refatoração do componente",
  "fileSize": 1024,
  "linesChanged": 15
}
```

## 6. Características Implementadas

✅ **Sistema de permissões** com 3 níveis (Owner/Editor/Reader)  
✅ **Sistema de convites** com tokens e expiração  
✅ **Activity logging** completo com metadados  
✅ **Controle de colaboração** entre usuários  
✅ **Auditoria detalhada** de todas as ações  
✅ **Permissões temporárias** com data de expiração  
✅ **Rastreamento de IP** e User Agent  
✅ **Estados de convite** bem definidos  

## 7. Próximos Passos

**Parte 3**: Entidades Auxiliares
- ModuleItemTag
- WorkspaceTag  
- WorkspaceVersion
- ItemVersion
- WorkspaceNavigationState
- SystemParameter

**Validação desta Parte**:
- [ ] Entidades de colaboração compilam sem erros
- [ ] Enums de atividades estão definidos
- [ ] Relationships com User e Workspace estão corretos
- [ ] Campos obrigatórios estão marcados
- [ ] Validações de data estão implementadas

## 8. Notas Importantes

⚠️ **User entity** deve existir da Fase 1 (Identity)  
⚠️ **Email service** será necessário para convites (pode ser implementado depois)  
⚠️ **Token generation** será implementado nos services  
⚠️ **Activity log cleanup** será implementado como job background  
⚠️ **Permission validation** será implementada nos services  

Esta parte estabelece a **base completa de colaboração** para o sistema de workspace. A próxima parte irá complementar com entidades auxiliares para organização e versionamento.