# Prompt Completo para Implementação do Backend .NET Core 8 - IDE com Autenticação e Workspace

## Contexto do Projeto

Você é um desenvolvedor experiente em .NET Core e precisa implementar o backend completo de uma aplicação IDE web inovadora. O frontend está sendo desenvolvido em **React 19 + TypeScript + Ant Design 5.0**, e você deve criar uma API robusta que se integre perfeitamente com essa stack moderna.

Esta IDE não trabalha com arquivos tradicionais, mas sim com **módulos conceituais** que agrupam **itens editáveis** suportados por diferentes tipos de editores no frontend.

## Especificações Técnicas

### Stack Tecnológica
- **.NET Core 8** com **Minimal API**
- **Entity Framework Core** com **PostgreSQL**
- **AutoMapper** para mapeamento de objetos
- **FluentValidation** para validação de dados
- **SignalR** para colaboração em tempo real
- **Docker** para containerização
- **Swagger/OpenAPI** para documentação

### Arquitetura do Projeto
Implemente seguindo **Clean Architecture** com a seguinte estrutura:

```
IDE.Backend/
├── src/
│   ├── IDE.API/                    # Minimal API, SignalR Hubs e configurações
│   ├── IDE.Application/            # Casos de uso, DTOs e Interfaces
│   ├── IDE.Domain/                 # Entidades, Value Objects e regras de negócio
│   ├── IDE.Infrastructure/         # Persistência, SignalR e serviços externos
│   └── IDE.Shared/                 # Utilitários, extensões e constantes
├── tests/
│   ├── IDE.UnitTests/
│   └── IDE.IntegrationTests/
├── docker-compose.yml
├── Dockerfile
└── README.md
```

## Módulo de Autenticação Completo

### Especificações de Autenticação

#### Métodos Suportados
- **JWT Bearer Tokens** (principal)
- **OAuth 2.0 / OpenID Connect** (Google, GitHub, Microsoft)
- **API Keys** (para integrações programáticas)

#### Configuração de Segurança
- **Access Tokens**: 15 minutos de duração
- **Refresh Tokens**: 7 dias, rotação automática
- **Armazenamento Frontend**: LocalStorage
- **Hash de Senhas**: BCrypt com salt
- **Rate Limiting**: 5 tentativas/minuto por IP
- **CORS**: Configurado para React frontend

### Endpoints de Autenticação

```
POST /api/auth/register              # Registro de usuário
POST /api/auth/login                 # Login tradicional  
POST /api/auth/refresh               # Renovar tokens
POST /api/auth/logout                # Logout (invalidar refresh token)
POST /api/auth/forgot-password       # Solicitar reset de senha
POST /api/auth/reset-password        # Confirmar reset de senha
GET  /api/auth/me                    # Obter dados do usuário atual
POST /api/auth/google                # Login com Google OAuth
POST /api/auth/github                # Login com GitHub OAuth  
POST /api/auth/microsoft             # Login com Microsoft OAuth
GET  /api/auth/apikeys               # Listar API keys do usuário
POST /api/auth/apikeys               # Criar nova API key
DELETE /api/auth/apikeys/{id}        # Revogar API key
```

### Entidades de Autenticação

```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Avatar { get; set; }
    public bool EmailVerified { get; set; }
    public UserPlan Plan { get; set; } = UserPlan.Free;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<Workspace> OwnedWorkspaces { get; set; }
    public List<WorkspacePermission> WorkspacePermissions { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; }
    public List<ApiKey> ApiKeys { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string DeviceInfo { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}

public class ApiKey
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Key { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}

public enum UserPlan
{
    Free = 0,
    Premium = 1,
    Enterprise = 2
}
```

## Módulo de Workspace Completo

### Conceito de Workspace na IDE

Um **Workspace** é um ambiente de trabalho conceitual que contém:
- **Itens com Módulos**: Documentos editáveis categorizados por módulo (string)
- **Fases de Desenvolvimento**: Customizáveis por workspace (ex: DEV → PO → QA → PROD)
- **Versionamento Semântico**: 1.0.0, 1.1.0, 2.0.0, etc.
- **Tags**: Organização tanto por workspace quanto por item individual

### Estrutura Hierárquica
```
Workspace "Meu Projeto v1.2.0" (Fases: DEV → Review → QA → PROD)
├── Item "HomePage Component" [Módulo: "Frontend"] [Tags: "React", "UI"]
├── Item "UserProfile Component" [Módulo: "Frontend"] [Tags: "React", "Profile"]  
├── Item "API Integration" [Módulo: "Frontend"] [Tags: "REST", "Integration"]
├── Item "User Controller" [Módulo: "Backend"] [Tags: "API", "Auth"]
├── Item "Database Schema" [Módulo: "Backend"] [Tags: "SQL", "Schema"]
├── Item "Authentication Logic" [Módulo: "Backend"] [Tags: "Security", "JWT"]
├── Item "UI Mockups" [Módulo: "Design"] [Tags: "Figma", "Wireframe"]
├── Item "Color Palette" [Módulo: "Design"] [Tags: "Branding", "Colors"]
└── Item "Brand Guidelines" [Módulo: "Design"] [Tags: "Branding", "Guidelines"]
```

### Endpoints de Workspace

```
# Workspace Management
GET    /api/workspaces                           # Listar workspaces do usuário
POST   /api/workspaces                           # Criar novo workspace
GET    /api/workspaces/{id}                      # Obter workspace específico
PUT    /api/workspaces/{id}                      # Atualizar workspace
DELETE /api/workspaces/{id}                      # Deletar workspace
POST   /api/workspaces/{id}/archive               # Arquivar workspace
POST   /api/workspaces/{id}/restore               # Restaurar workspace
POST   /api/workspaces/{id}/duplicate             # Duplicar workspace
GET    /api/workspaces/{id}/versions              # Listar versões do workspace

# Item Management (direto no workspace)
GET    /api/workspaces/{id}/items                 # Listar itens do workspace
POST   /api/workspaces/{id}/items                 # Criar item
GET    /api/workspaces/{id}/items/{itemId}        # Obter item específico  
PUT    /api/workspaces/{id}/items/{itemId}        # Atualizar item
DELETE /api/workspaces/{id}/items/{itemId}        # Deletar item

# Item Tags Management
GET    /api/workspaces/{id}/items/{itemId}/tags   # Listar tags do item
POST   /api/workspaces/{id}/items/{itemId}/tags   # Adicionar tag ao item
DELETE /api/workspaces/{id}/items/{itemId}/tags/{tagId} # Remover tag do item

# Collaboration
GET    /api/workspaces/{id}/permissions           # Listar permissões
POST   /api/workspaces/{id}/permissions           # Convidar colaborador
PUT    /api/workspaces/{id}/permissions/{userId}  # Atualizar permissão
DELETE /api/workspaces/{id}/permissions/{userId}  # Remover colaborador
POST   /api/workspaces/{id}/invitations/{token}/accept # Aceitar convite

# Versioning & Phases (fases customizáveis)
GET    /api/workspaces/{id}/phases                # Listar fases do workspace
POST   /api/workspaces/{id}/phases                # Criar nova fase
PUT    /api/workspaces/{id}/phases/{phaseId}      # Atualizar fase
DELETE /api/workspaces/{id}/phases/{phaseId}      # Deletar fase
POST   /api/workspaces/{id}/promote               # Promover para próxima fase
POST   /api/workspaces/{id}/demote                # Rebaixar para fase anterior
GET    /api/workspaces/{id}/history               # Histórico de mudanças
POST   /api/workspaces/{id}/versions              # Criar nova versão semântica

# Search & Organization  
GET    /api/workspaces/search                     # Buscar workspaces
GET    /api/workspaces/{id}/tags                  # Listar tags do workspace
POST   /api/workspaces/{id}/tags                  # Adicionar tag
DELETE /api/workspaces/{id}/tags/{tagId}          # Remover tag
```

### Entidades de Workspace

```csharp
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
}

public class ModuleItem
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public string Module { get; set; }          // String para categorizar o item
    public string EditorType { get; set; }     // "code", "markdown", "json", "visual", etc.
    public string Language { get; set; }       // "typescript", "javascript", "json", etc.
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public List<ItemVersion> Versions { get; set; } = new();
    public List<ModuleItemTag> Tags { get; set; } = new();
}

public class ModuleItemTag
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ModuleItemId { get; set; }
    public ModuleItem ModuleItem { get; set; }
}

public class WorkspacePhase
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Color { get; set; }
    public int Order { get; set; }
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public List<WorkspaceVersion> Versions { get; set; } = new();
}

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

public class WorkspaceTag
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
}

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

## Sistema de Configurações e Planos

### Configurações Parametrizáveis

```csharp
public class SystemConfiguration
{
    public Guid Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
    public ConfigType Type { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PlanLimits
{
    public Guid Id { get; set; }
    public UserPlan Plan { get; set; }
    public int MaxWorkspaces { get; set; } = 10;
    public long MaxStoragePerWorkspace { get; set; } = 10 * 1024 * 1024; // 10MB
    public long MaxItemSize { get; set; } = 5 * 1024 * 1024; // 5MB
    public int MaxCollaboratorsPerWorkspace { get; set; } = 5;
    public bool CanUseApiKeys { get; set; } = false;
    public bool CanExportWorkspaces { get; set; } = false;
}

public enum ConfigType
{
    String = 0,
    Integer = 1,
    Boolean = 2,
    Json = 3
}
```

### Endpoints de Configuração

```
GET    /api/admin/config                    # Listar todas as configurações
PUT    /api/admin/config/{key}              # Atualizar configuração
GET    /api/admin/plans                     # Listar planos e limites
PUT    /api/admin/plans/{plan}              # Atualizar limites do plano
GET    /api/admin/stats                     # Estatísticas da aplicação
```

## Colaboração em Tempo Real com SignalR

### Funcionalidades de Tempo Real
- **Edição Simultânea**: Múltiplos usuários editando o mesmo item
- **Chat em Workspace**: Comunicação entre colaboradores
- **Notificações**: Mudanças, convites, promoções de fase
- **Presença**: Usuários ativos no workspace
- **Cursores em Tempo Real**: Posição dos cursores de outros usuários

### Hubs SignalR

```csharp
public class WorkspaceHub : Hub
{
    // Conexão e Grupos
    public async Task JoinWorkspace(string workspaceId);
    public async Task LeaveWorkspace(string workspaceId);
    
    // Edição Colaborativa
    public async Task JoinItem(string itemId);
    public async Task LeaveItem(string itemId);
    public async Task SendEdit(string itemId, EditorChange change);
    public async Task SendCursor(string itemId, CursorPosition position);
    
    // Chat
    public async Task SendMessage(string workspaceId, ChatMessage message);
    public async Task TypingIndicator(string workspaceId, bool isTyping);
    
    // Notificações
    public async Task SendNotification(string workspaceId, Notification notification);
}

public class EditorChange
{
    public string Type { get; set; } // "insert", "delete", "replace"
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Content { get; set; }
    public string UserId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CursorPosition
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserColor { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Endpoints SignalR
```
# Conectar ao Hub
ws://localhost:8503/hubs/workspace

# Eventos do Cliente para Servidor
JoinWorkspace(workspaceId)
LeaveWorkspace(workspaceId)
JoinItem(itemId)
LeaveItem(itemId)
SendEdit(itemId, change)
SendCursor(itemId, position)
SendMessage(workspaceId, message)

# Eventos do Servidor para Cliente  
UserJoined(user)
UserLeft(userId)
ItemEdit(itemId, change)
CursorUpdate(itemId, position)
MessageReceived(message)
NotificationReceived(notification)
```

## Integração Frontend-Backend

### Configurações de API
- **Base URL**: `http://localhost:8503` (variável de ambiente)
- **CORS**: Permitir origem React (`http://localhost:5173`)
- **Headers**: Authorization, Content-Type, X-Requested-With
- **Métodos**: GET, POST, PUT, DELETE, OPTIONS

### Estrutura de Resposta Padronizada

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PaginatedResponse<T> : ApiResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
```

### Middleware de Tratamento de Erros

```csharp
public class ErrorHandlingMiddleware
{
    // Capturar todas as exceções
    // Retornar respostas padronizadas
    // Log estruturado de erros
    // Diferentes respostas para Dev/Prod
}
```

## Configuração de Ambiente

### Docker Configuration

````dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8503

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/IDE.API/IDE.API.csproj", "src/IDE.API/"]
COPY ["src/IDE.Application/IDE.Application.csproj", "src/IDE.Application/"]
COPY ["src/IDE.Domain/IDE.Domain.csproj", "src/IDE.Domain/"]
COPY ["src/IDE.Infrastructure/IDE.Infrastructure.csproj", "src/IDE.Infrastructure/"]
COPY ["src/IDE.Shared/IDE.Shared.csproj", "src/IDE.Shared/"]
RUN dotnet restore "src/IDE.API/IDE.API.csproj"
COPY . .
WORKDIR "/src/src/IDE.API"
RUN dotnet build "IDE.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IDE.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IDE.API.dll"]
````

````yaml
# docker-compose.yml
version: '3.8'
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: ide_db
      POSTGRES_USER: ide_user
      POSTGRES_PASSWORD: ide_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  api:
    build: .
    ports:
      - "8503:8503"
    depends_on:
      - postgres
      - redis
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=ide_db;Username=ide_user;Password=ide_password
      - Redis__ConnectionString=redis:6379
      - JWT__Secret=your-super-secret-jwt-key-here
      - Frontend__BaseUrl=http://localhost:5173

volumes:
  postgres_data:
````

### Variáveis de Ambiente

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=ide_db;Username=ide_user;Password=ide_password"
  },
  "JWT": {
    "Secret": "your-super-secret-jwt-key-here-with-at-least-32-characters",
    "Issuer": "IDE.API",
    "Audience": "IDE.Frontend",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "OAuth": {
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret"
    },
    "GitHub": {
      "ClientId": "your-github-client-id", 
      "ClientSecret": "your-github-client-secret"
    },
    "Microsoft": {
      "ClientId": "your-microsoft-client-id",
      "ClientSecret": "your-microsoft-client-secret"
    }
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Frontend": {
    "BaseUrl": "http://localhost:5173"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromName": "IDE Platform"
  },
  "RateLimit": {
    "General": {
      "PermitLimit": 100,
      "Window": "00:01:00"
    },
    "Auth": {
      "PermitLimit": 5,
      "Window": "00:01:00"
    }
  }
}
```

## Implementação por Fases

### Fase 1: Fundação e Autenticação 
1. **Setup do Projeto**
   - Criar estrutura Clean Architecture
   - Configurar Entity Framework + PostgreSQL
   - Configurar Docker e docker-compose
   - Setup de testes unitários

2. **Sistema de Autenticação**
   - Implementar entidades User, RefreshToken, ApiKey
   - Criar serviços de autenticação JWT
   - Implementar OAuth providers (Google, GitHub, Microsoft)
   - Endpoints de register, login, refresh, logout
   - Middleware de autenticação e autorização
   - Rate limiting para rotas de auth

3. **Configurações Base**
   - Sistema de configurações parametrizáveis
   - Planos e limites por usuário
   - Middleware de tratamento de erros
   - Estrutura de resposta padronizada

### Fase 2: Workspace Core 
1. **Entidades de Workspace**
   - Implementar Workspace, ModuleItem (com propriedade Module string)
   - Implementar WorkspacePhase como entidade customizável
   - Implementar ModuleItemTag para tags de itens
   - Sistema de permissões e convites (incluindo Reader)
   - Tags de workspace e organização
   - Migrations automáticas

2. **CRUD de Workspace**
   - Endpoints completos de workspace
   - Gestão direta de itens (sem módulos como entidade)
   - Gestão de fases customizáveis por workspace
   - Sistema de convites com aprovação
   - Busca e filtros avançados

3. **Versionamento e Fases**
   - Sistema de fases customizáveis por workspace
   - Versionamento semântico
   - Histórico de mudanças
   - Promoção/rebaixamento entre fases dinâmicas

### Fase 3: Colaboração em Tempo Real
1. **SignalR Hub**
   - Implementar WorkspaceHub
   - Gestão de conexões e grupos
   - Eventos de entrada/saída de usuários

2. **Edição Colaborativa**
   - Sincronização de mudanças em tempo real
   - Gestão de cursores múltiplos
   - Resolução de conflitos básica
   - Debounce e otimizações

3. **Chat e Notificações**
   - Sistema de chat por workspace
   - Notificações em tempo real
   - Indicadores de presença
   - Histórico de mensagens

### Fase 4: Otimização e Finalização 
1. **Performance e Caching**
   - Redis para cache de sessões
   - Otimização de queries EF Core
   - Pagination eficiente
   - Compressão de responses

2. **Monitoramento e Logs**
   - Logging estruturado (Serilog)
   - Health checks
   - Métricas de performance
   - Swagger documentation completa

3. **Segurança e Deploy**
   - Validação rigorosa de inputs
   - Sanitização de dados
   - HTTPS obrigatório
   - Configuração para produção

## Requisitos de Qualidade

### Testes
- **Cobertura mínima**: 85%
- **Testes unitários**: Todos os services e handlers
- **Testes de integração**: Endpoints principais
- **Testes de carga**: SignalR e endpoints críticos

### Performance
- **Tempo de resposta**: < 200ms para 95% das requests
- **Throughput**: Suportar 1000 usuários simultâneos
- **Memory usage**: < 512MB para instância básica
- **Database**: Queries otimizadas com índices apropriados

### Segurança
- **Autenticação**: JWT + OAuth 2.0 seguro
- **Autorização**: Verificação rigorosa de permissões
- **Rate Limiting**: Proteção contra abuso
- **Input Validation**: FluentValidation em todos os endpoints
- **SQL Injection**: EF Core com queries parametrizadas
- **XSS Protection**: Sanitização de conteúdo de usuário

### Documentação
- **OpenAPI/Swagger**: Documentação completa da API
- **README**: Instruções de setup e deploy
- **Architecture Decision Records**: Decisões de arquitetura documentadas
- **Code Comments**: JSDoc para métodos complexos

## Critérios de Sucesso

1. **Funcionalidade Completa**
   - Todos os endpoints funcionais
   - Autenticação OAuth funcionando
   - SignalR colaboração em tempo real
   - Sistema de fases e versionamento

2. **Integração Frontend**
   - API consumível pelo React frontend
   - CORS configurado corretamente
   - Estrutura de dados compatível
   - WebSocket SignalR funcionando

3. **Qualidade de Código**
   - Clean Architecture implementada
   - Testes com boa cobertura
   - Logs estruturados
   - Tratamento de erros robusto

4. **Deploy Ready**
   - Docker funcionando
   - Variáveis de ambiente configuradas
   - Database migrations automáticas
   - Pronto para produção

**Implemente este backend completo seguindo rigorosamente todas as especificações, garantindo máxima qualidade, performance e integração perfeita com o frontend React 19 + TypeScript + Ant Design.**
