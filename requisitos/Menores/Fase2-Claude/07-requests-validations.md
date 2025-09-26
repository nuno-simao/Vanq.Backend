# Parte 7: Requests e Validações - Workspace Core

## Contexto
Esta é a **Parte 7 de 12** da Fase 2 (Workspace Core). Aqui criaremos todos os Request objects e suas respectivas validações usando FluentValidation.

**Pré-requisitos**: Parte 6 (DTOs Básicos) deve estar concluída

**Dependências**: DTOs e entidades de domínio

**Próxima parte**: Parte 8 - Redis Cache System

## Objetivos desta Parte
✅ Criar Request objects para todas as operações  
✅ Implementar validações com FluentValidation  
✅ Definir regras de negócio nas validações  
✅ Configurar validações customizadas  
✅ Preparar contratos para controllers  

## 1. Requests de Workspace

### 1.1 CreateWorkspaceRequest.cs

#### IDE.Application/Workspace/Requests/CreateWorkspaceRequest.cs
```csharp
using System.Collections.Generic;

namespace IDE.Application.Workspace.Requests
{
    public class CreateWorkspaceRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> DefaultPhases { get; set; } = new() { "Development" };
        public List<string> InitialTags { get; set; } = new();
        public bool CreateSampleItems { get; set; } = false;
    }
}
```

### 1.2 UpdateWorkspaceRequest.cs

#### IDE.Application/Workspace/Requests/UpdateWorkspaceRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class UpdateWorkspaceRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
```

### 1.3 WorkspaceSearchRequest.cs

#### IDE.Application/Workspace/Requests/WorkspaceSearchRequest.cs
```csharp
using System.Collections.Generic;

namespace IDE.Application.Workspace.Requests
{
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
}
```

## 2. Requests de ModuleItem

### 2.1 CreateModuleItemRequest.cs

#### IDE.Application/Workspace/Requests/CreateModuleItemRequest.cs
```csharp
using System;
using System.Collections.Generic;

namespace IDE.Application.Workspace.Requests
{
    public class CreateModuleItemRequest
    {
        public string Name { get; set; }
        public string Content { get; set; } = "";
        public string Module { get; set; }
        public string Type { get; set; }
        public Guid? ParentId { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
```

### 2.2 UpdateModuleItemRequest.cs

#### IDE.Application/Workspace/Requests/UpdateModuleItemRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class UpdateModuleItemRequest
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public string Module { get; set; }
        public string Type { get; set; }
        public int VersionNumber { get; set; }
    }
}
```

### 2.3 UpdateItemDataRequest.cs

#### IDE.Application/Workspace/Requests/UpdateItemDataRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class UpdateItemDataRequest
    {
        public string Content { get; set; }
        public int VersionNumber { get; set; }
    }
}
```

### 2.4 SaveItemRequest.cs

#### IDE.Application/Workspace/Requests/SaveItemRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class SaveItemRequest
    {
        public int VersionNumber { get; set; }
        public string Comment { get; set; }
    }
}
```

### 2.5 ItemSearchRequest.cs

#### IDE.Application/Workspace/Requests/ItemSearchRequest.cs
```csharp
using System;
using System.Collections.Generic;

namespace IDE.Application.Workspace.Requests
{
    public class ItemSearchRequest
    {
        public string Query { get; set; }
        public string Module { get; set; }
        public string Type { get; set; }
        public Guid? ParentId { get; set; }
        public List<string> Tags { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SortBy { get; set; } = "UpdatedAt";
        public string SortDirection { get; set; } = "desc";
    }
}
```

## 3. Requests de Colaboração

### 3.1 InviteUserRequest.cs

#### IDE.Application/Workspace/Requests/InviteUserRequest.cs
```csharp
using IDE.Domain.Enums;

namespace IDE.Application.Workspace.Requests
{
    public class InviteUserRequest
    {
        public string Email { get; set; }
        public PermissionLevel Level { get; set; } = PermissionLevel.Reader;
        public string Message { get; set; }
        public string InvitedUserName { get; set; }
    }
}
```

### 3.2 UpdatePermissionRequest.cs

#### IDE.Application/Workspace/Requests/UpdatePermissionRequest.cs
```csharp
using IDE.Domain.Enums;

namespace IDE.Application.Workspace.Requests
{
    public class UpdatePermissionRequest
    {
        public PermissionLevel Level { get; set; }
    }
}
```

### 3.3 AcceptInvitationRequest.cs

#### IDE.Application/Workspace/Requests/AcceptInvitationRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class AcceptInvitationRequest
    {
        public string Token { get; set; }
    }
}
```

## 4. Requests de Fases e Versionamento

### 4.1 CreateWorkspacePhaseRequest.cs

#### IDE.Application/Workspace/Requests/CreateWorkspacePhaseRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class CreateWorkspacePhaseRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; } = "#52c41a";
        public int Order { get; set; }
    }
}
```

### 4.2 CreateVersionRequest.cs

#### IDE.Application/Workspace/Requests/CreateVersionRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class CreateVersionRequest
    {
        public string Version { get; set; }
        public string Description { get; set; }
    }
}
```

### 4.3 SetNavigationStateRequest.cs

#### IDE.Application/Workspace/Requests/SetNavigationStateRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class SetNavigationStateRequest
    {
        public string ModuleId { get; set; }
        public object State { get; set; }
    }
}
```

## 5. Requests de Tags

### 5.1 CreateWorkspaceTagRequest.cs

#### IDE.Application/Workspace/Requests/CreateWorkspaceTagRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class CreateWorkspaceTagRequest
    {
        public string Name { get; set; }
        public string Color { get; set; } = "#1890ff";
        public string Description { get; set; }
    }
}
```

### 5.2 AddItemTagRequest.cs

#### IDE.Application/Workspace/Requests/AddItemTagRequest.cs
```csharp
namespace IDE.Application.Workspace.Requests
{
    public class AddItemTagRequest
    {
        public string Name { get; set; }
        public string Color { get; set; } = "#1890ff";
        public string Description { get; set; }
    }
}
```

## 6. Validadores FluentValidation

### 6.1 CreateWorkspaceRequestValidator.cs

#### IDE.Application/Workspace/Validators/CreateWorkspaceRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;

namespace IDE.Application.Workspace.Validators
{
    public class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
    {
        public CreateWorkspaceRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Nome é obrigatório")
                .Length(3, 200).WithMessage("Nome deve ter entre 3 e 200 caracteres")
                .Matches(@"^[a-zA-Z0-9\s\-_\.]+$").WithMessage("Nome contém caracteres inválidos");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Descrição não pode exceder 1000 caracteres");

            RuleFor(x => x.DefaultPhases)
                .NotNull().WithMessage("Fases padrão são obrigatórias")
                .Must(phases => phases.Count > 0).WithMessage("Deve ter pelo menos uma fase")
                .Must(phases => phases.All(p => !string.IsNullOrWhiteSpace(p)))
                    .WithMessage("Nomes de fases não podem estar vazios");

            RuleForEach(x => x.DefaultPhases)
                .Length(2, 100).WithMessage("Nome da fase deve ter entre 2 e 100 caracteres");

            RuleForEach(x => x.InitialTags)
                .Length(1, 50).WithMessage("Nome da tag deve ter entre 1 e 50 caracteres");
        }
    }
}
```

### 6.2 CreateModuleItemRequestValidator.cs

#### IDE.Application/Workspace/Validators/CreateModuleItemRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;
using System.Collections.Generic;

namespace IDE.Application.Workspace.Validators
{
    public class CreateModuleItemRequestValidator : AbstractValidator<CreateModuleItemRequest>
    {
        private readonly List<string> _validModules = new() 
        { 
            "Documents", "Tests", "Environments", "API", "Frontend", "Backend", "Config" 
        };
        
        private readonly List<string> _validTypes = new() 
        { 
            "typescript", "javascript", "json", "markdown", "html", "css", "sql", 
            "text", "uml-diagram", "database", "database-table", "config", "yaml", "xml"
        };

        public CreateModuleItemRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Nome é obrigatório")
                .Length(1, 255).WithMessage("Nome deve ter entre 1 e 255 caracteres")
                .Must(NotContainInvalidChars).WithMessage("Nome contém caracteres inválidos");

            RuleFor(x => x.Module)
                .NotEmpty().WithMessage("Módulo é obrigatório")
                .Must(module => _validModules.Contains(module))
                    .WithMessage($"Módulo deve ser um dos seguintes: {string.Join(", ", _validModules)}");

            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Tipo é obrigatório")
                .Must(type => _validTypes.Contains(type))
                    .WithMessage($"Tipo deve ser um dos seguintes: {string.Join(", ", _validTypes)}");

            RuleFor(x => x.Content)
                .Must(content => content == null || content.Length <= 10_000_000)
                    .WithMessage("Conteúdo não pode exceder 10MB");

            RuleForEach(x => x.Tags)
                .Length(1, 50).WithMessage("Nome da tag deve ter entre 1 e 50 caracteres");
        }

        private bool NotContainInvalidChars(string name)
        {
            var invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '/', '\\' };
            return !invalidChars.Any(name.Contains);
        }
    }
}
```

### 6.3 UpdateModuleItemRequestValidator.cs

#### IDE.Application/Workspace/Validators/UpdateModuleItemRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;

namespace IDE.Application.Workspace.Validators
{
    public class UpdateModuleItemRequestValidator : AbstractValidator<UpdateModuleItemRequest>
    {
        public UpdateModuleItemRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Nome é obrigatório")
                .Length(1, 255).WithMessage("Nome deve ter entre 1 e 255 caracteres");

            RuleFor(x => x.Module)
                .NotEmpty().WithMessage("Módulo é obrigatório")
                .Length(1, 100).WithMessage("Módulo deve ter entre 1 e 100 caracteres");

            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Tipo é obrigatório")
                .Length(1, 100).WithMessage("Tipo deve ter entre 1 e 100 caracteres");

            RuleFor(x => x.VersionNumber)
                .GreaterThan(0).WithMessage("Número da versão deve ser maior que 0");

            RuleFor(x => x.Content)
                .Must(content => content == null || content.Length <= 10_000_000)
                    .WithMessage("Conteúdo não pode exceder 10MB");
        }
    }
}
```

### 6.4 InviteUserRequestValidator.cs

#### IDE.Application/Workspace/Validators/InviteUserRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;
using IDE.Domain.Enums;

namespace IDE.Application.Workspace.Validators
{
    public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
    {
        public InviteUserRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email é obrigatório")
                .EmailAddress().WithMessage("Email deve ter formato válido")
                .MaximumLength(255).WithMessage("Email não pode exceder 255 caracteres");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Nível de permissão inválido")
                .NotEqual(PermissionLevel.Owner).WithMessage("Não é possível convidar como Owner");

            RuleFor(x => x.Message)
                .MaximumLength(500).WithMessage("Mensagem não pode exceder 500 caracteres");

            RuleFor(x => x.InvitedUserName)
                .MaximumLength(255).WithMessage("Nome não pode exceder 255 caracteres");
        }
    }
}
```

### 6.5 ItemSearchRequestValidator.cs

#### IDE.Application/Workspace/Validators/ItemSearchRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;

namespace IDE.Application.Workspace.Validators
{
    public class ItemSearchRequestValidator : AbstractValidator<ItemSearchRequest>
    {
        public ItemSearchRequestValidator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage("Página deve ser maior que 0");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Tamanho da página deve ser maior que 0")
                .LessThanOrEqualTo(100).WithMessage("Tamanho da página não pode exceder 100");

            RuleFor(x => x.SortBy)
                .Must(sortBy => string.IsNullOrEmpty(sortBy) || IsValidSortField(sortBy))
                    .WithMessage("Campo de ordenação inválido");

            RuleFor(x => x.SortDirection)
                .Must(direction => string.IsNullOrEmpty(direction) || 
                      direction.ToLower() is "asc" or "desc")
                    .WithMessage("Direção deve ser 'asc' ou 'desc'");

            RuleFor(x => x.Query)
                .MaximumLength(500).WithMessage("Query não pode exceder 500 caracteres");

            RuleFor(x => x.Module)
                .MaximumLength(100).WithMessage("Módulo não pode exceder 100 caracteres");

            RuleFor(x => x.Type)
                .MaximumLength(100).WithMessage("Tipo não pode exceder 100 caracteres");
        }

        private bool IsValidSortField(string field)
        {
            var validFields = new[] 
            { 
                "Name", "UpdatedAt", "CreatedAt", "Size", "Module", "Type" 
            };
            return validFields.Contains(field, StringComparer.OrdinalIgnoreCase);
        }
    }
}
```

### 6.6 SetNavigationStateRequestValidator.cs

#### IDE.Application/Workspace/Validators/SetNavigationStateRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;
using System.Text.Json;

namespace IDE.Application.Workspace.Validators
{
    public class SetNavigationStateRequestValidator : AbstractValidator<SetNavigationStateRequest>
    {
        public SetNavigationStateRequestValidator()
        {
            RuleFor(x => x.ModuleId)
                .NotEmpty().WithMessage("ModuleId é obrigatório")
                .Length(1, 100).WithMessage("ModuleId deve ter entre 1 e 100 caracteres");

            RuleFor(x => x.State)
                .NotNull().WithMessage("Estado é obrigatório")
                .Must(BeValidJson).WithMessage("Estado deve ser um JSON válido")
                .Must(NotExceedSizeLimit).WithMessage("Estado não pode exceder 100KB");
        }

        private bool BeValidJson(object state)
        {
            try
            {
                JsonSerializer.Serialize(state);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool NotExceedSizeLimit(object state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state);
                return json.Length <= 100_000; // 100KB
            }
            catch
            {
                return false;
            }
        }
    }
}
```

## 7. Validadores Adicionais

### 7.1 CreateWorkspacePhaseRequestValidator.cs

#### IDE.Application/Workspace/Validators/CreateWorkspacePhaseRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;
using System.Text.RegularExpressions;

namespace IDE.Application.Workspace.Validators
{
    public class CreateWorkspacePhaseRequestValidator : AbstractValidator<CreateWorkspacePhaseRequest>
    {
        public CreateWorkspacePhaseRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Nome é obrigatório")
                .Length(2, 100).WithMessage("Nome deve ter entre 2 e 100 caracteres");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Descrição não pode exceder 500 caracteres");

            RuleFor(x => x.Color)
                .NotEmpty().WithMessage("Cor é obrigatória")
                .Must(BeValidHexColor).WithMessage("Cor deve estar no formato hexadecimal (#RRGGBB)");

            RuleFor(x => x.Order)
                .GreaterThanOrEqualTo(0).WithMessage("Ordem deve ser maior ou igual a 0");
        }

        private bool BeValidHexColor(string color)
        {
            return !string.IsNullOrEmpty(color) && 
                   Regex.IsMatch(color, @"^#[0-9A-Fa-f]{6}$");
        }
    }
}
```

### 7.2 CreateVersionRequestValidator.cs

#### IDE.Application/Workspace/Validators/CreateVersionRequestValidator.cs
```csharp
using FluentValidation;
using IDE.Application.Workspace.Requests;
using System.Text.RegularExpressions;

namespace IDE.Application.Workspace.Validators
{
    public class CreateVersionRequestValidator : AbstractValidator<CreateVersionRequest>
    {
        public CreateVersionRequestValidator()
        {
            RuleFor(x => x.Version)
                .NotEmpty().WithMessage("Versão é obrigatória")
                .Must(BeValidSemanticVersion).WithMessage("Versão deve seguir o formato semântico (x.y.z)");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Descrição não pode exceder 500 caracteres");
        }

        private bool BeValidSemanticVersion(string version)
        {
            return !string.IsNullOrEmpty(version) && 
                   Regex.IsMatch(version, @"^\d+\.\d+\.\d+$");
        }
    }
}
```

## 8. Configuração de Validação

### 8.1 Registro no DI Container

#### Program.cs (adição)
```csharp
using FluentValidation;
using IDE.Application.Workspace.Validators;

// Registro dos validadores
builder.Services.AddScoped<IValidator<CreateWorkspaceRequest>, CreateWorkspaceRequestValidator>();
builder.Services.AddScoped<IValidator<CreateModuleItemRequest>, CreateModuleItemRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateModuleItemRequest>, UpdateModuleItemRequestValidator>();
builder.Services.AddScoped<IValidator<InviteUserRequest>, InviteUserRequestValidator>();
builder.Services.AddScoped<IValidator<ItemSearchRequest>, ItemSearchRequestValidator>();
builder.Services.AddScoped<IValidator<SetNavigationStateRequest>, SetNavigationStateRequestValidator>();
builder.Services.AddScoped<IValidator<CreateWorkspacePhaseRequest>, CreateWorkspacePhaseRequestValidator>();
builder.Services.AddScoped<IValidator<CreateVersionRequest>, CreateVersionRequestValidator>();

// Ou usar assembly scanning
builder.Services.AddValidatorsFromAssemblyContaining<CreateWorkspaceRequestValidator>();
```

## 9. Middleware de Validação

### 9.1 ValidationMiddleware.cs

#### IDE.API/Middleware/ValidationMiddleware.cs
```csharp
using FluentValidation;
using System.Net;
using System.Text.Json;

namespace IDE.API.Middleware
{
    public class ValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public ValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                await HandleValidationExceptionAsync(context, ex);
            }
        }

        private static async Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = "Erro de validação",
                errors = ex.Errors.Select(e => new
                {
                    field = e.PropertyName,
                    message = e.ErrorMessage,
                    attemptedValue = e.AttemptedValue
                })
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
```

## 10. Características Implementadas

✅ **Request objects completos** para todas as operações  
✅ **Validações robustas** with FluentValidation  
✅ **Regras de negócio** nas validações  
✅ **Validações customizadas** (JSON, hex color, semantic version)  
✅ **Mensagens de erro** em português  
✅ **Middleware de validação** para tratamento global  
✅ **Registro automático** no DI container  
✅ **Paginação validada** com limites apropriados  

## 11. Próximos Passos

**Parte 8**: Redis Cache System
- RedisCacheService implementation
- Cache strategies e key patterns
- TTL configurations
- Performance optimization

**Validação desta Parte**:
- [ ] Request objects compilam sem erros
- [ ] Validadores funcionam corretamente  
- [ ] Middleware de validação está configurado
- [ ] Mensagens de erro são apropriadas
- [ ] Registro no DI funciona

## 12. Notas Importantes

⚠️ **FluentValidation** deve ser instalado via NuGet  
⚠️ **Middleware** deve ser registrado no pipeline  
⚠️ **Validações** são executadas antes dos controllers  
⚠️ **Mensagens** podem ser localizadas conforme necessário  
⚠️ **Performance** - validações complexas podem impactar  
⚠️ **Testes unitários** devem cobrir todos os validadores  

Esta parte estabelece **contratos sólidos e validados** para todas as operações da API. A próxima parte implementará o sistema de cache Redis para performance otimizada.