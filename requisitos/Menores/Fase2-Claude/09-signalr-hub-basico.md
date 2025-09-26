# Parte 9: SignalR Hub Básico - Workspace Core

## Contexto
Esta é a **Parte 9 de 12** da Fase 2 (Workspace Core). Aqui implementaremos o sistema SignalR para comunicação em tempo real, notificações e sincronização básica entre usuários.

**Pré-requisitos**: Parte 8 (Redis Cache System) deve estar concluída

**Dependências**: Microsoft.AspNetCore.SignalR, Redis backplane

**Próxima parte**: Parte 10 - Workspace Services

## Objetivos desta Parte
✅ Implementar WorkspaceHub para comunicação em tempo real  
✅ Configurar grupos de usuários por workspace  
✅ Sistema de notificações em tempo real  
✅ Sincronização básica de estado  
✅ Integração com autenticação JWT  

## 1. SignalR Hub Implementation

### 1.1 IWorkspaceHubService.cs

#### IDE.Application/Common/Interfaces/IWorkspaceHubService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDE.Application.Common.Interfaces
{
    public interface IWorkspaceHubService
    {
        // User management
        Task JoinWorkspaceAsync(string connectionId, Guid userId, Guid workspaceId);
        Task LeaveWorkspaceAsync(string connectionId, Guid userId, Guid workspaceId);
        Task LeaveAllWorkspacesAsync(string connectionId, Guid userId);
        
        // Notifications
        Task SendNotificationToWorkspaceAsync(Guid workspaceId, object notification, Guid? excludeUserId = null);
        Task SendNotificationToUserAsync(Guid userId, object notification);
        Task SendNotificationToUsersAsync(List<Guid> userIds, object notification);
        
        // Item synchronization
        Task NotifyItemChangedAsync(Guid workspaceId, Guid itemId, string action, object data = null, Guid? excludeUserId = null);
        Task NotifyItemStatusChangedAsync(Guid workspaceId, Guid itemId, string status, Guid userId);
        
        // Workspace events
        Task NotifyWorkspaceUpdatedAsync(Guid workspaceId, object changes, Guid? excludeUserId = null);
        Task NotifyUserJoinedWorkspaceAsync(Guid workspaceId, Guid userId, string userName);
        Task NotifyUserLeftWorkspaceAsync(Guid workspaceId, Guid userId, string userName);
        
        // Navigation synchronization
        Task SyncNavigationStateAsync(Guid workspaceId, Guid userId, string moduleId, object navigationState);
        
        // Activity tracking
        Task NotifyUserActivityAsync(Guid workspaceId, Guid userId, string activity, object data = null);
        
        // Connection info
        Task<List<Guid>> GetWorkspaceUsersAsync(Guid workspaceId);
        Task<int> GetWorkspaceConnectionCountAsync(Guid workspaceId);
        Task<bool> IsUserOnlineAsync(Guid userId);
    }
}
```

### 1.2 WorkspaceHub.cs

#### IDE.API/Hubs/WorkspaceHub.cs
```csharp
using IDE.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace IDE.API.Hubs
{
    [Authorize]
    public class WorkspaceHub : Hub
    {
        private readonly IWorkspaceHubService _hubService;
        private readonly ILogger<WorkspaceHub> _logger;
        private readonly IRedisCacheService _cacheService;

        public WorkspaceHub(
            IWorkspaceHubService hubService,
            ILogger<WorkspaceHub> logger,
            IRedisCacheService cacheService)
        {
            _hubService = hubService;
            _logger = logger;
            _cacheService = cacheService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId != null)
            {
                await _cacheService.SetAsync($"connection:{Context.ConnectionId}", new ConnectionInfo
                {
                    UserId = userId.Value,
                    ConnectedAt = DateTime.UtcNow,
                    UserAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"]
                }, TimeSpan.FromDays(1));

                _logger.LogInformation("Usuário {UserId} conectado com connectionId {ConnectionId}", userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = GetUserId();
            if (userId != null)
            {
                await _hubService.LeaveAllWorkspacesAsync(Context.ConnectionId, userId.Value);
                await _cacheService.RemoveAsync($"connection:{Context.ConnectionId}");
                
                _logger.LogInformation("Usuário {UserId} desconectado. ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        [HubMethodName("JoinWorkspace")]
        public async Task JoinWorkspaceAsync(string workspaceId)
        {
            var userId = GetUserId();
            if (userId == null || !Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                await Clients.Caller.SendAsync("Error", "Invalid parameters");
                return;
            }

            try
            {
                await _hubService.JoinWorkspaceAsync(Context.ConnectionId, userId.Value, workspaceGuid);
                await Clients.Caller.SendAsync("JoinedWorkspace", workspaceId);
                
                // Notificar outros usuários
                var userName = Context.User?.Identity?.Name ?? "Usuário";
                await _hubService.NotifyUserJoinedWorkspaceAsync(workspaceGuid, userId.Value, userName);
                
                _logger.LogDebug("Usuário {UserId} entrou no workspace {WorkspaceId}", userId, workspaceGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao entrar no workspace {WorkspaceId}", workspaceGuid);
                await Clients.Caller.SendAsync("Error", "Não foi possível entrar no workspace");
            }
        }

        [HubMethodName("LeaveWorkspace")]
        public async Task LeaveWorkspaceAsync(string workspaceId)
        {
            var userId = GetUserId();
            if (userId == null || !Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                await Clients.Caller.SendAsync("Error", "Invalid parameters");
                return;
            }

            try
            {
                await _hubService.LeaveWorkspaceAsync(Context.ConnectionId, userId.Value, workspaceGuid);
                await Clients.Caller.SendAsync("LeftWorkspace", workspaceId);
                
                // Notificar outros usuários
                var userName = Context.User?.Identity?.Name ?? "Usuário";
                await _hubService.NotifyUserLeftWorkspaceAsync(workspaceGuid, userId.Value, userName);
                
                _logger.LogDebug("Usuário {UserId} saiu do workspace {WorkspaceId}", userId, workspaceGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sair do workspace {WorkspaceId}", workspaceGuid);
                await Clients.Caller.SendAsync("Error", "Não foi possível sair do workspace");
            }
        }

        [HubMethodName("SyncNavigationState")]
        public async Task SyncNavigationStateAsync(string workspaceId, string moduleId, object navigationState)
        {
            var userId = GetUserId();
            if (userId == null || !Guid.TryParse(workspaceId, out var workspaceGuid))
                return;

            try
            {
                await _hubService.SyncNavigationStateAsync(workspaceGuid, userId.Value, moduleId, navigationState);
                _logger.LogDebug("Estado de navegação sincronizado para usuário {UserId}, workspace {WorkspaceId}, módulo {ModuleId}", 
                    userId, workspaceGuid, moduleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar estado de navegação");
            }
        }

        [HubMethodName("NotifyActivity")]
        public async Task NotifyActivityAsync(string workspaceId, string activity, object data = null)
        {
            var userId = GetUserId();
            if (userId == null || !Guid.TryParse(workspaceId, out var workspaceGuid))
                return;

            try
            {
                await _hubService.NotifyUserActivityAsync(workspaceGuid, userId.Value, activity, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao notificar atividade do usuário");
            }
        }

        [HubMethodName("GetWorkspaceUsers")]
        public async Task<List<Guid>> GetWorkspaceUsersAsync(string workspaceId)
        {
            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new List<Guid>();

            try
            {
                return await _hubService.GetWorkspaceUsersAsync(workspaceGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar usuários do workspace");
                return new List<Guid>();
            }
        }

        [HubMethodName("Ping")]
        public async Task PingAsync()
        {
            await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
        }

        private Guid? GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public class ConnectionInfo
    {
        public Guid UserId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public string UserAgent { get; set; }
    }
}
```

### 1.3 WorkspaceHubService.cs

#### IDE.Infrastructure/SignalR/WorkspaceHubService.cs
```csharp
using IDE.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using IDE.API.Hubs;

namespace IDE.Infrastructure.SignalR
{
    public class WorkspaceHubService : IWorkspaceHubService
    {
        private readonly IHubContext<WorkspaceHub> _hubContext;
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<WorkspaceHubService> _logger;

        public WorkspaceHubService(
            IHubContext<WorkspaceHub> hubContext,
            IRedisCacheService cacheService,
            ILogger<WorkspaceHubService> logger)
        {
            _hubContext = hubContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task JoinWorkspaceAsync(string connectionId, Guid userId, Guid workspaceId)
        {
            try
            {
                await _hubContext.Groups.AddToGroupAsync(connectionId, GetWorkspaceGroupName(workspaceId));
                
                // Cache da conexão
                var connectionKey = $"workspace_connection:{workspaceId}:{userId}:{connectionId}";
                await _cacheService.SetAsync(connectionKey, new
                {
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    ConnectionId = connectionId,
                    JoinedAt = DateTime.UtcNow
                }, TimeSpan.FromDays(1));

                // Adicionar à lista de usuários online do workspace
                await _cacheService.AddToListAsync($"workspace_users:{workspaceId}", new
                {
                    UserId = userId,
                    ConnectionId = connectionId,
                    JoinedAt = DateTime.UtcNow
                });

                _logger.LogDebug("Usuário {UserId} entrou no grupo do workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar usuário {UserId} ao workspace {WorkspaceId}", userId, workspaceId);
                throw;
            }
        }

        public async Task LeaveWorkspaceAsync(string connectionId, Guid userId, Guid workspaceId)
        {
            try
            {
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, GetWorkspaceGroupName(workspaceId));
                
                // Remover conexão do cache
                var connectionKey = $"workspace_connection:{workspaceId}:{userId}:{connectionId}";
                await _cacheService.RemoveAsync(connectionKey);

                // Remover da lista de usuários online (note: esta é uma simplificação, 
                // em produção seria melhor usar um Set do Redis)
                await _cacheService.RemoveAsync($"workspace_user_connection:{workspaceId}:{userId}:{connectionId}");

                _logger.LogDebug("Usuário {UserId} saiu do grupo do workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover usuário {UserId} do workspace {WorkspaceId}", userId, workspaceId);
                throw;
            }
        }

        public async Task LeaveAllWorkspacesAsync(string connectionId, Guid userId)
        {
            try
            {
                // Buscar todos os workspaces que o usuário está conectado
                var pattern = $"workspace_connection:*:{userId}:{connectionId}";
                var keys = await _cacheService.GetKeysAsync(pattern, 1000);

                var tasks = keys.Select(async key =>
                {
                    // Extract workspaceId from key pattern
                    var parts = key.Split(':');
                    if (parts.Length >= 4 && Guid.TryParse(parts[2], out var workspaceId))
                    {
                        await LeaveWorkspaceAsync(connectionId, userId, workspaceId);
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogDebug("Usuário {UserId} removido de todos os workspaces", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover usuário {UserId} de todos os workspaces", userId);
                throw;
            }
        }

        public async Task SendNotificationToWorkspaceAsync(Guid workspaceId, object notification, Guid? excludeUserId = null)
        {
            try
            {
                var groupName = GetWorkspaceGroupName(workspaceId);
                await _hubContext.Clients.Group(groupName).SendAsync("Notification", notification);
                
                _logger.LogDebug("Notificação enviada para workspace {WorkspaceId}", workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação para workspace {WorkspaceId}", workspaceId);
            }
        }

        public async Task SendNotificationToUserAsync(Guid userId, object notification)
        {
            try
            {
                var userGroupName = GetUserGroupName(userId);
                await _hubContext.Clients.Group(userGroupName).SendAsync("Notification", notification);
                
                _logger.LogDebug("Notificação enviada para usuário {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação para usuário {UserId}", userId);
            }
        }

        public async Task SendNotificationToUsersAsync(List<Guid> userIds, object notification)
        {
            try
            {
                var tasks = userIds.Select(userId => SendNotificationToUserAsync(userId, notification));
                await Task.WhenAll(tasks);
                
                _logger.LogDebug("Notificação enviada para {Count} usuários", userIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificações para múltiplos usuários");
            }
        }

        public async Task NotifyItemChangedAsync(Guid workspaceId, Guid itemId, string action, object data = null, Guid? excludeUserId = null)
        {
            var notification = new
            {
                Type = "ItemChanged",
                WorkspaceId = workspaceId,
                ItemId = itemId,
                Action = action,
                Data = data,
                Timestamp = DateTime.UtcNow
            };

            await SendNotificationToWorkspaceAsync(workspaceId, notification, excludeUserId);
        }

        public async Task NotifyItemStatusChangedAsync(Guid workspaceId, Guid itemId, string status, Guid userId)
        {
            var notification = new
            {
                Type = "ItemStatusChanged",
                WorkspaceId = workspaceId,
                ItemId = itemId,
                Status = status,
                UserId = userId,
                Timestamp = DateTime.UtcNow
            };

            await SendNotificationToWorkspaceAsync(workspaceId, notification);
        }

        public async Task NotifyWorkspaceUpdatedAsync(Guid workspaceId, object changes, Guid? excludeUserId = null)
        {
            var notification = new
            {
                Type = "WorkspaceUpdated",
                WorkspaceId = workspaceId,
                Changes = changes,
                Timestamp = DateTime.UtcNow
            };

            await SendNotificationToWorkspaceAsync(workspaceId, notification, excludeUserId);
        }

        public async Task NotifyUserJoinedWorkspaceAsync(Guid workspaceId, Guid userId, string userName)
        {
            var notification = new
            {
                Type = "UserJoined",
                WorkspaceId = workspaceId,
                UserId = userId,
                UserName = userName,
                Timestamp = DateTime.UtcNow
            };

            await SendNotificationToWorkspaceAsync(workspaceId, notification, userId);
        }

        public async Task NotifyUserLeftWorkspaceAsync(Guid workspaceId, Guid userId, string userName)
        {
            var notification = new
            {
                Type = "UserLeft",
                WorkspaceId = workspaceId,
                UserId = userId,
                UserName = userName,
                Timestamp = DateTime.UtcNow
            };

            await SendNotificationToWorkspaceAsync(workspaceId, notification, userId);
        }

        public async Task SyncNavigationStateAsync(Guid workspaceId, Guid userId, string moduleId, object navigationState)
        {
            try
            {
                // Salvar estado no cache
                var key = $"navigation:user:{userId}:workspace:{workspaceId}:module:{moduleId}";
                await _cacheService.SetAsync(key, navigationState, TimeSpan.FromMinutes(30));

                // Notificar outros usuários sobre mudança de navegação
                var notification = new
                {
                    Type = "NavigationStateChanged",
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    ModuleId = moduleId,
                    NavigationState = navigationState,
                    Timestamp = DateTime.UtcNow
                };

                await SendNotificationToWorkspaceAsync(workspaceId, notification, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar estado de navegação");
            }
        }

        public async Task NotifyUserActivityAsync(Guid workspaceId, Guid userId, string activity, object data = null)
        {
            var notification = new
            {
                Type = "UserActivity",
                WorkspaceId = workspaceId,
                UserId = userId,
                Activity = activity,
                Data = data,
                Timestamp = DateTime.UtcNow
            };

            await SendNotificationToWorkspaceAsync(workspaceId, notification, userId);
        }

        public async Task<List<Guid>> GetWorkspaceUsersAsync(Guid workspaceId)
        {
            try
            {
                var users = await _cacheService.GetListAsync<dynamic>($"workspace_users:{workspaceId}");
                return users.Select(u => (Guid)u.UserId).Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar usuários do workspace {WorkspaceId}", workspaceId);
                return new List<Guid>();
            }
        }

        public async Task<int> GetWorkspaceConnectionCountAsync(Guid workspaceId)
        {
            try
            {
                var users = await GetWorkspaceUsersAsync(workspaceId);
                return users.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao contar conexões do workspace {WorkspaceId}", workspaceId);
                return 0;
            }
        }

        public async Task<bool> IsUserOnlineAsync(Guid userId)
        {
            try
            {
                var pattern = $"workspace_connection:*:{userId}:*";
                var keys = await _cacheService.GetKeysAsync(pattern, 1);
                return keys.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar se usuário {UserId} está online", userId);
                return false;
            }
        }

        private static string GetWorkspaceGroupName(Guid workspaceId) => $"workspace_{workspaceId}";
        private static string GetUserGroupName(Guid userId) => $"user_{userId}";
    }
}
```

## 2. SignalR Configuration and Middleware

### 2.1 SignalR Authentication

#### IDE.API/Middleware/SignalRJwtMiddleware.cs
```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IDE.API.Middleware
{
    public class SignalRJwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public SignalRJwtMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/workspaceHub"))
            {
                var token = GetTokenFromQuery(context) ?? GetTokenFromHeader(context);
                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var principal = ValidateToken(token);
                        if (principal != null)
                        {
                            context.User = principal;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue
                        Console.WriteLine($"JWT validation error: {ex.Message}");
                    }
                }
            }

            await _next(context);
        }

        private string GetTokenFromQuery(HttpContext context)
        {
            return context.Request.Query["access_token"].FirstOrDefault();
        }

        private string GetTokenFromHeader(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }
            return null;
        }

        private ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]);

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["JwtSettings:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var validatedToken);
            return principal;
        }
    }
}
```

## 3. Real-time Notifications

### 3.1 NotificationService.cs

#### IDE.Infrastructure/SignalR/NotificationService.cs
```csharp
using IDE.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IDE.Infrastructure.SignalR
{
    public class NotificationService : INotificationService
    {
        private readonly IWorkspaceHubService _hubService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IWorkspaceHubService hubService,
            ILogger<NotificationService> logger)
        {
            _hubService = hubService;
            _logger = logger;
        }

        public async Task SendWorkspaceNotificationAsync(Guid workspaceId, NotificationDto notification)
        {
            try
            {
                await _hubService.SendNotificationToWorkspaceAsync(workspaceId, notification);
                _logger.LogDebug("Notificação enviada para workspace {WorkspaceId}: {Type}", workspaceId, notification.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação para workspace {WorkspaceId}", workspaceId);
            }
        }

        public async Task SendUserNotificationAsync(Guid userId, NotificationDto notification)
        {
            try
            {
                await _hubService.SendNotificationToUserAsync(userId, notification);
                _logger.LogDebug("Notificação enviada para usuário {UserId}: {Type}", userId, notification.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação para usuário {UserId}", userId);
            }
        }

        public async Task SendBulkNotificationAsync(List<Guid> userIds, NotificationDto notification)
        {
            try
            {
                await _hubService.SendNotificationToUsersAsync(userIds, notification);
                _logger.LogDebug("Notificação enviada para {Count} usuários: {Type}", userIds.Count, notification.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação em massa");
            }
        }
    }

    public class NotificationDto
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public string Severity { get; set; } = "info"; // info, warning, error, success
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool RequiresAction { get; set; }
        public string ActionUrl { get; set; }
    }
}
```

### 3.2 INotificationService.cs

#### IDE.Application/Common/Interfaces/INotificationService.cs
```csharp
using IDE.Infrastructure.SignalR;

namespace IDE.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task SendWorkspaceNotificationAsync(Guid workspaceId, NotificationDto notification);
        Task SendUserNotificationAsync(Guid userId, NotificationDto notification);
        Task SendBulkNotificationAsync(List<Guid> userIds, NotificationDto notification);
    }
}
```

## 4. Configuration

### 4.1 Program.cs Configuration

#### Program.cs (adições)
```csharp
using IDE.API.Hubs;
using IDE.API.Middleware;
using IDE.Infrastructure.SignalR;

// SignalR Configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
})
.AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"), options =>
{
    options.Configuration.ChannelPrefix = "IDE_SignalR";
});

// SignalR Services
builder.Services.AddScoped<IWorkspaceHubService, WorkspaceHubService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Build app
var app = builder.Build();

// Middlewares
app.UseMiddleware<SignalRJwtMiddleware>();

// SignalR Hub
app.MapHub<WorkspaceHub>("/workspaceHub", options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
});
```

### 4.2 Client-side Connection Example

#### JavaScript/TypeScript Client Example
```typescript
// workspaceSignalR.ts
import * as signalR from '@microsoft/signalr';

export class WorkspaceSignalRService {
    private connection: signalR.HubConnection;
    private token: string;

    constructor(token: string) {
        this.token = token;
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('/workspaceHub', {
                accessTokenFactory: () => this.token,
                transport: signalR.HttpTransportType.WebSockets
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.previousRetryCount === 0) return 0;
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount - 1), 30000);
                }
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupEventHandlers();
    }

    private setupEventHandlers(): void {
        this.connection.on('Notification', (notification) => {
            console.log('Received notification:', notification);
            this.handleNotification(notification);
        });

        this.connection.on('JoinedWorkspace', (workspaceId) => {
            console.log(`Joined workspace: ${workspaceId}`);
        });

        this.connection.on('LeftWorkspace', (workspaceId) => {
            console.log(`Left workspace: ${workspaceId}`);
        });

        this.connection.on('Error', (error) => {
            console.error('SignalR Error:', error);
        });

        this.connection.on('Pong', (timestamp) => {
            console.log('Pong received:', timestamp);
        });

        this.connection.onreconnecting((error) => {
            console.log('SignalR reconnecting:', error);
        });

        this.connection.onreconnected((connectionId) => {
            console.log('SignalR reconnected:', connectionId);
        });

        this.connection.onclose((error) => {
            console.log('SignalR connection closed:', error);
        });
    }

    async start(): Promise<void> {
        try {
            await this.connection.start();
            console.log('SignalR connection started');
        } catch (error) {
            console.error('Error starting SignalR connection:', error);
            throw error;
        }
    }

    async stop(): Promise<void> {
        try {
            await this.connection.stop();
            console.log('SignalR connection stopped');
        } catch (error) {
            console.error('Error stopping SignalR connection:', error);
        }
    }

    async joinWorkspace(workspaceId: string): Promise<void> {
        await this.connection.invoke('JoinWorkspace', workspaceId);
    }

    async leaveWorkspace(workspaceId: string): Promise<void> {
        await this.connection.invoke('LeaveWorkspace', workspaceId);
    }

    async syncNavigationState(workspaceId: string, moduleId: string, navigationState: any): Promise<void> {
        await this.connection.invoke('SyncNavigationState', workspaceId, moduleId, navigationState);
    }

    async notifyActivity(workspaceId: string, activity: string, data?: any): Promise<void> {
        await this.connection.invoke('NotifyActivity', workspaceId, activity, data);
    }

    async getWorkspaceUsers(workspaceId: string): Promise<string[]> {
        return await this.connection.invoke('GetWorkspaceUsers', workspaceId);
    }

    async ping(): Promise<void> {
        await this.connection.invoke('Ping');
    }

    private handleNotification(notification: any): void {
        switch (notification.Type) {
            case 'ItemChanged':
                this.onItemChanged(notification);
                break;
            case 'WorkspaceUpdated':
                this.onWorkspaceUpdated(notification);
                break;
            case 'UserJoined':
                this.onUserJoined(notification);
                break;
            case 'UserLeft':
                this.onUserLeft(notification);
                break;
            case 'NavigationStateChanged':
                this.onNavigationStateChanged(notification);
                break;
            default:
                console.log('Unknown notification type:', notification.Type);
        }
    }

    private onItemChanged(notification: any): void {
        // Handle item changes
        console.log('Item changed:', notification);
    }

    private onWorkspaceUpdated(notification: any): void {
        // Handle workspace updates
        console.log('Workspace updated:', notification);
    }

    private onUserJoined(notification: any): void {
        // Handle user joined
        console.log('User joined:', notification);
    }

    private onUserLeft(notification: any): void {
        // Handle user left
        console.log('User left:', notification);
    }

    private onNavigationStateChanged(notification: any): void {
        // Handle navigation state changes
        console.log('Navigation state changed:', notification);
    }
}
```

## 5. Próximos Passos

**Parte 10**: Workspace Services
- WorkspaceService implementation
- Business logic layer
- CRUD operations
- Permission validation

**Validação desta Parte**:
- [ ] SignalR Hub está funcionando
- [ ] Autenticação JWT funciona com SignalR
- [ ] Usuários conseguem entrar/sair de workspaces
- [ ] Notificações são enviadas corretamente
- [ ] Redis backplane está configurado

## 6. Características Implementadas

✅ **WorkspaceHub completo** com autenticação JWT  
✅ **Grupos por workspace** para comunicação direcionada  
✅ **Sistema de notificações** em tempo real  
✅ **Sincronização de estado** de navegação  
✅ **Tracking de usuários** online por workspace  
✅ **Reconexão automática** no cliente  
✅ **Redis backplane** para escalabilidade  
✅ **Error handling** robusto  

## 7. Notas Importantes

⚠️ **Redis backplane** é essencial para múltiplas instâncias  
⚠️ **JWT token** deve ser passado na query string ou header  
⚠️ **Connection management** é crítico para performance  
⚠️ **Error handling** deve ser implementado no cliente  
⚠️ **Rate limiting** pode ser necessário em produção  
⚠️ **Monitoring** das conexões SignalR é recomendado  

Esta parte estabelece **comunicação em tempo real robusta** entre usuários do mesmo workspace. A próxima parte implementará os serviços de negócio para workspaces.