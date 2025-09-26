# Parte 6: DTOs Básicos - Workspace Core

## Contexto
Esta é a **Parte 6 de 12** da Fase 2 (Workspace Core). Aqui criaremos todos os DTOs (Data Transfer Objects) necessários para a comunicação entre as camadas da aplicação.

**Pré-requisitos**: Partes 1-5 (Entidades e Migration) devem estar concluídas

**Dependências**: Entidades de domínio criadas

**Próxima parte**: Parte 7 - Requests e Validações

## Objetivos desta Parte
✅ Criar DTOs para todas as entidades de workspace  
✅ Configurar AutoMapper profiles  
✅ Criar response objects e pagination  
✅ Definir DTOs de listagem e detalhes  
✅ Preparar contratos para APIs  

## 1. DTOs Principais

### 1.1 WorkspaceDto.cs

#### IDE.Application/Workspace/DTOs/WorkspaceDto.cs
```csharp
using System;
using System.Collections.Generic;

namespace IDE.Application.Workspace.DTOs
{
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
        public List<WorkspacePermissionDto> Permissions { get; set; } = new();
        public List<WorkspaceTagDto> Tags { get; set; } = new();
        public List<WorkspacePhaseDto> Phases { get; set; } = new();
        
        // Métricas
        public int TotalItems { get; set; }
        public long TotalSize { get; set; }
        public int TotalUsers { get; set; }
        public DateTime LastActivity { get; set; }
        
        // Permissões do usuário atual
        public PermissionLevel CurrentUserPermission { get; set; }
    }
    
    // DTO simplificado para listagens
    public class WorkspaceSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string SemanticVersion { get; set; }
        public string CurrentPhaseName { get; set; }
        public string CurrentPhaseColor { get; set; }
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string OwnerName { get; set; }
        public int TotalItems { get; set; }
        public int TotalUsers { get; set; }
        public PermissionLevel CurrentUserPermission { get; set; }
    }
}
```

### 1.2 ModuleItemDto.cs

#### IDE.Application/Workspace/DTOs/ModuleItemDto.cs
```csharp
using System;
using System.Collections.Generic;

namespace IDE.Application.Workspace.DTOs
{
    public class ModuleItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public string Module { get; set; }
        public string Type { get; set; }
        public Guid? ParentId { get; set; }
        public int VersionNumber { get; set; }
        public long Size { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ModuleItemTagDto> Tags { get; set; } = new();
        public List<ModuleItemDto> Children { get; set; } = new();
        
        // Metadados
        public int TotalVersions { get; set; }
        public DateTime LastSavedAt { get; set; }
        public string LastSavedByName { get; set; }
    }
    
    // DTO simplificado para árvore/listagem
    public class ModuleItemSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Module { get; set; }
        public string Type { get; set; }
        public Guid? ParentId { get; set; }
        public long Size { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool HasChildren { get; set; }
        public int ChildrenCount { get; set; }
    }
}
```

## 2. DTOs de Colaboração

### 2.1 WorkspacePermissionDto.cs

#### IDE.Application/Workspace/DTOs/WorkspacePermissionDto.cs
```csharp
using System;
using IDE.Domain.Enums;

namespace IDE.Application.Workspace.DTOs
{
    public class WorkspacePermissionDto
    {
        public Guid Id { get; set; }
        public PermissionLevel Level { get; set; }
        public string LevelName => Level.ToString();
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public UserDto User { get; set; }
        public UserDto GrantedBy { get; set; }
    }
}
```

### 2.2 WorkspaceInvitationDto.cs

#### IDE.Application/Workspace/DTOs/WorkspaceInvitationDto.cs
```csharp
using System;
using IDE.Domain.Enums;

namespace IDE.Application.Workspace.DTOs
{
    public class WorkspaceInvitationDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public PermissionLevel Level { get; set; }
        public string LevelName => Level.ToString();
        public InvitationStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public UserDto InvitedBy { get; set; }
        public UserDto AcceptedBy { get; set; }
        public string Message { get; set; }
        public string InvitedUserName { get; set; }
        
        // Propriedades calculadas
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        public int DaysUntilExpiry => (ExpiresAt - DateTime.UtcNow).Days;
    }
}
```

### 2.3 ActivityLogDto.cs

#### IDE.Application/Workspace/DTOs/ActivityLogDto.cs
```csharp
using System;
using System.Text.Json;

namespace IDE.Application.Workspace.DTOs
{
    public class ActivityLogDto
    {
        public Guid Id { get; set; }
        public string Action { get; set; }
        public string ActionDescription => GetActionDescription();
        public string Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto User { get; set; }
        public Guid? EntityId { get; set; }
        public string EntityType { get; set; }
        public string Category { get; set; }
        public int? OldVersionNumber { get; set; }
        public int? NewVersionNumber { get; set; }
        public object Metadata { get; set; }
        
        private string GetActionDescription()
        {
            return Action switch
            {
                "WORKSPACE_CREATED" => "criou o workspace",
                "WORKSPACE_UPDATED" => "atualizou o workspace",
                "ITEM_CREATED" => "criou um item",
                "ITEM_UPDATED" => "atualizou um item",
                "ITEM_DELETED" => "deletou um item",
                "USER_INVITED" => "convidou um usuário",
                "PERMISSION_GRANTED" => "concedeu permissão",
                _ => Action.ToLower().Replace("_", " ")
            };
        }
    }
}
```

## 3. DTOs Auxiliares

### 3.1 WorkspacePhaseDto.cs

#### IDE.Application/Workspace/DTOs/WorkspacePhaseDto.cs
```csharp
using System;

namespace IDE.Application.Workspace.DTOs
{
    public class WorkspacePhaseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public int Order { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public int VersionCount { get; set; }
    }
}
```

### 3.2 Tags DTOs

#### IDE.Application/Workspace/DTOs/TagDtos.cs
```csharp
using System;

namespace IDE.Application.Workspace.DTOs
{
    public class ModuleItemTagDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public bool IsSystem { get; set; }
    }

    public class WorkspaceTagDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public int UsageCount { get; set; }
        public bool IsActive { get; set; }
    }
}
```

### 3.3 Version DTOs

#### IDE.Application/Workspace/DTOs/VersionDtos.cs
```csharp
using System;

namespace IDE.Application.Workspace.DTOs
{
    public class WorkspaceVersionDto
    {
        public Guid Id { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public WorkspacePhaseDto Phase { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto CreatedBy { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsSnapshot { get; set; }
        public string ChangeType { get; set; }
        public int TotalItems { get; set; }
        public long TotalSize { get; set; }
        public object Metadata { get; set; }
    }

    public class ItemVersionDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserDto CreatedBy { get; set; }
        public int VersionNumber { get; set; }
        public long Size { get; set; }
        public string ChangeType { get; set; }
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public int LinesModified { get; set; }
        public string PreviousName { get; set; }
        public string PreviousModule { get; set; }
        public string PreviousType { get; set; }
    }
}
```

## 4. DTOs de Sistema

### 4.1 SystemParameterDto.cs

#### IDE.Application/Workspace/DTOs/SystemParameterDto.cs
```csharp
using System;

namespace IDE.Application.Workspace.DTOs
{
    public class SystemParameterDto
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string DataType { get; set; }
        public string DefaultValue { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsActive { get; set; }
        public int? MinValue { get; set; }
        public int? MaxValue { get; set; }
        public string Environment { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

### 4.2 WorkspaceNavigationStateDto.cs

#### IDE.Application/Workspace/DTOs/WorkspaceNavigationStateDto.cs
```csharp
using System;

namespace IDE.Application.Workspace.DTOs
{
    public class WorkspaceNavigationStateDto
    {
        public string ModuleId { get; set; }
        public object State { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Version { get; set; }
        public long Size { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
    }
}
```

## 5. Response Objects

### 5.1 PaginatedResponse.cs

#### IDE.Application/Common/DTOs/PaginatedResponse.cs
```csharp
using System;
using System.Collections.Generic;

namespace IDE.Application.Common.DTOs
{
    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
        
        // Metadados adicionais
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    // Response específico para items (mais campos)
    public class PaginatedItemsResponse<T> : PaginatedResponse<T>
    {
        public Dictionary<string, int> ModuleCounts { get; set; } = new();
        public Dictionary<string, int> TypeCounts { get; set; } = new();
        public List<string> AvailableTags { get; set; } = new();
        public long TotalSize { get; set; }
    }
}
```

### 5.2 ApiResponse.cs

#### IDE.Application/Common/DTOs/ApiResponse.cs
```csharp
using System.Collections.Generic;

namespace IDE.Application.Common.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        public static ApiResponse<T> SuccessResult(T data, string message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }
        
        public static ApiResponse<T> ErrorResult(string message, List<string> errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }
}
```

## 6. AutoMapper Profiles

### 6.1 WorkspaceProfile.cs

#### IDE.Application/Workspace/Profiles/WorkspaceProfile.cs
```csharp
using AutoMapper;
using IDE.Application.Workspace.DTOs;
using IDE.Domain.Entities.Workspace;
using IDE.Domain.Entities.System;

namespace IDE.Application.Workspace.Profiles
{
    public class WorkspaceProfile : Profile
    {
        public WorkspaceProfile()
        {
            // Workspace mappings
            CreateMap<Domain.Entities.Workspace.Workspace, WorkspaceDto>()
                .ForMember(dest => dest.TotalItems, opt => opt.MapFrom(src => src.Items.Count))
                .ForMember(dest => dest.TotalSize, opt => opt.MapFrom(src => src.Items.Sum(i => i.Size)))
                .ForMember(dest => dest.TotalUsers, opt => opt.MapFrom(src => src.Permissions.Count))
                .ForMember(dest => dest.LastActivity, opt => opt.MapFrom(src => 
                    src.Activities.OrderByDescending(a => a.CreatedAt).FirstOrDefault().CreatedAt));
                
            CreateMap<Domain.Entities.Workspace.Workspace, WorkspaceSummaryDto>()
                .ForMember(dest => dest.CurrentPhaseName, opt => opt.MapFrom(src => src.CurrentPhase.Name))
                .ForMember(dest => dest.CurrentPhaseColor, opt => opt.MapFrom(src => src.CurrentPhase.Color))
                .ForMember(dest => dest.OwnerName, opt => opt.MapFrom(src => src.Owner.UserName))
                .ForMember(dest => dest.TotalItems, opt => opt.MapFrom(src => src.Items.Count))
                .ForMember(dest => dest.TotalUsers, opt => opt.MapFrom(src => src.Permissions.Count));

            // ModuleItem mappings
            CreateMap<ModuleItem, ModuleItemDto>()
                .ForMember(dest => dest.TotalVersions, opt => opt.MapFrom(src => src.Versions.Count))
                .ForMember(dest => dest.LastSavedAt, opt => opt.MapFrom(src => 
                    src.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault().CreatedAt))
                .ForMember(dest => dest.LastSavedByName, opt => opt.MapFrom(src => 
                    src.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault().CreatedBy.UserName));
                    
            CreateMap<ModuleItem, ModuleItemSummaryDto>()
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(t => t.Name).ToList()))
                .ForMember(dest => dest.HasChildren, opt => opt.MapFrom(src => src.Children.Any()))
                .ForMember(dest => dest.ChildrenCount, opt => opt.MapFrom(src => src.Children.Count));

            // Collaboration mappings
            CreateMap<WorkspacePermission, WorkspacePermissionDto>();
            CreateMap<WorkspaceInvitation, WorkspaceInvitationDto>();
            CreateMap<ActivityLog, ActivityLogDto>()
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => 
                    string.IsNullOrEmpty(src.MetadataJson) ? null : 
                    System.Text.Json.JsonSerializer.Deserialize<object>(src.MetadataJson)));

            // Auxiliary mappings
            CreateMap<WorkspacePhase, WorkspacePhaseDto>()
                .ForMember(dest => dest.VersionCount, opt => opt.MapFrom(src => src.Versions.Count));
            CreateMap<ModuleItemTag, ModuleItemTagDto>();
            CreateMap<WorkspaceTag, WorkspaceTagDto>();
            CreateMap<WorkspaceVersion, WorkspaceVersionDto>()
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => 
                    string.IsNullOrEmpty(src.MetadataJson) ? null : 
                    System.Text.Json.JsonSerializer.Deserialize<object>(src.MetadataJson)));
            CreateMap<ItemVersion, ItemVersionDto>();

            // System mappings
            CreateMap<SystemParameter, SystemParameterDto>();
            CreateMap<WorkspaceNavigationState, WorkspaceNavigationStateDto>()
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => 
                    System.Text.Json.JsonSerializer.Deserialize<object>(src.StateJson)));
        }
    }
}
```

## 7. User DTO (Referência)

### 7.1 UserDto.cs

#### IDE.Application/Common/DTOs/UserDto.cs
```csharp
using System;

namespace IDE.Application.Common.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Plan { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        public string ProfilePictureUrl { get; set; }
    }
}
```

## 8. Próximos Passos

**Parte 7**: Requests e Validações
- CreateWorkspaceRequest, UpdateModuleItemRequest
- FluentValidation validators
- Request objects para todas as operações

**Validação desta Parte**:
- [ ] DTOs compilam sem erros
- [ ] AutoMapper profile funciona
- [ ] Response objects estão completos
- [ ] Mapeamentos estão corretos

## 9. Características Implementadas

✅ **DTOs completos** para todas as entidades  
✅ **DTOs resumidos** para listagens otimizadas  
✅ **AutoMapper profiles** configurados  
✅ **Response objects** com paginação  
✅ **Metadados calculados** automaticamente  
✅ **Propriedades derivadas** nos DTOs  
✅ **Separação de responsabilidades** clara  
✅ **JSON serialization** configurada  

## 10. Notas Importantes

⚠️ **AutoMapper** deve ser registrado no DI  
⚠️ **JSON serialization** pode ser customizada  
⚠️ **DTOs grandes** considerar lazy loading  
⚠️ **Circular references** evitadas no mapping  
⚠️ **Performance** - DTOs resumidos para listas  
⚠️ **Versionamento** - manter compatibilidade  

Esta parte estabelece todos os **contratos de dados** da Fase 2. A próxima parte criará os requests e validações para as operações de API.