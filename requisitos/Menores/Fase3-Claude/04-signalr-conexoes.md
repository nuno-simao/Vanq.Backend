# Fase 3.4: SignalR Hub - Conexões

## Implementação do Hub SignalR para Colaboração

Esta parte implementa a **estrutura base do WorkspaceHub**, **gestão de conexões** e **sistemas de grupos** para colaboração em tempo real. É o coração da comunicação entre os clientes.

**Pré-requisitos**: Partes 3.1, 3.2 e 3.3 implementadas

## 1. Estrutura Base do WorkspaceHub

### 1.1 Hub Principal

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs
```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using IDE.Application.Services.Collaboration;
using IDE.Application.Services.Chat;
using IDE.Application.Services.Notifications;

namespace IDE.Infrastructure.Realtime
{
    /// <summary>
    /// Hub SignalR para colaboração em tempo real
    /// </summary>
    [Authorize]
    public partial class WorkspaceHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserPresenceService _presenceService;
        private readonly ICollaborationMetricsService _metricsService;
        private readonly IRateLimitingService _rateLimitingService;
        private readonly ICollaborationAuditService _auditService;
        private readonly ILogger<WorkspaceHub> _logger;
        private readonly IHubContext<WorkspaceHub> _hubContext;
        private readonly ISystemParameterService _systemParameterService;

        // NOTA: Interfaces implementadas no Grupo 3 (Services)
        // - IUserPresenceService: Grupo 3.7 (Presença e usuários ativos)
        // - ICollaborationMetricsService: Grupo 3.8 (Métricas de colaboração)
        // - IRateLimitingService: Grupo 3.8 (Rate limiting por plano)
        // - ICollaborationAuditService: Grupo 3.8 (Auditoria de ações)
        // - ISystemParameterService: Grupo 3.8 (Parâmetros do sistema)

        public WorkspaceHub(
            ApplicationDbContext context,
            IUserPresenceService presenceService,
            ICollaborationMetricsService metricsService,
            IRateLimitingService rateLimitingService,
            ICollaborationAuditService auditService,
            ILogger<WorkspaceHub> logger,
            IHubContext<WorkspaceHub> hubContext,
            ISystemParameterService systemParameterService)
        {
            _context = context;
            _presenceService = presenceService;
            _metricsService = metricsService;
            _rateLimitingService = rateLimitingService;
            _auditService = auditService;
            _logger = logger;
            _hubContext = hubContext;
            _systemParameterService = systemParameterService;
        }

        /// <summary>
        /// Usuário entra em um workspace para colaboração
        /// </summary>
        public async Task JoinWorkspace(string workspaceId)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Rate limiting check
                if (!await _rateLimitingService.CheckLimitAsync(userId, "workspace_join"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para workspace join",
                        Code = "RATE_LIMIT_WORKSPACE_JOIN"
                    });
                    return;
                }

                // Verificar permissão de acesso ao workspace
                if (!await HasWorkspaceAccess(workspaceGuid, userId))
                {
                    await _auditService.LogAsync(AuditAction.AccessDenied, "workspace", workspaceId, userId);
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Acesso negado ao workspace",
                        Code = "WORKSPACE_ACCESS_DENIED"
                    });
                    return;
                }

                // Verificar limite de usuários simultâneos no workspace
                var activeUsers = await _presenceService.GetWorkspaceActiveCountAsync(workspaceGuid);
                var maxUsers = await _systemParameterService.GetIntAsync(
                    CollaborationParameters.COLLABORATION_MAX_USERS_PER_WORKSPACE);
                
                if (activeUsers >= maxUsers)
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "WorkspaceFull",
                        Message = $"Workspace atingiu limite máximo de {maxUsers} usuários",
                        Code = "WORKSPACE_USER_LIMIT",
                        MaxUsers = maxUsers,
                        CurrentUsers = activeUsers
                    });
                    return;
                }

                // Entrar no grupo do workspace (com suporte a sharding)
                var shardGroup = GetShardGroup(workspaceId);
                await Groups.AddToGroupAsync(Context.ConnectionId, shardGroup);

                // Registrar presença do usuário
                await _presenceService.SetUserPresenceAsync(
                    workspaceGuid, 
                    userId, 
                    Context.ConnectionId, 
                    UserPresenceStatus.Online);

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.JoinWorkspace, "workspace", workspaceId, userId,
                    JsonSerializer.Serialize(new { 
                        connectionId = Context.ConnectionId,
                        shard = shardGroup,
                        userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString()
                    }));

                // Métricas
                await _metricsService.IncrementAsync("workspace_joins", 1, new { 
                    workspaceId, 
                    shard = shardGroup 
                });

                // Obter dados do usuário atual
                var currentUser = await GetCurrentUserDto(userId);

                // Enviar confirmação para o usuário que entrou
                await Clients.Caller.SendAsync("WorkspaceJoined", new
                {
                    WorkspaceId = workspaceId,
                    Shard = shardGroup,
                    JoinedAt = DateTime.UtcNow,
                    User = currentUser,
                    ActiveUsers = await _presenceService.GetWorkspacePresenceAsync(workspaceGuid)
                });

                // Notificar outros usuários no workspace
                await Clients.OthersInGroup(shardGroup).SendAsync("UserJoined", new
                {
                    WorkspaceId = workspaceId,
                    User = currentUser,
                    Presence = new UserPresenceDto
                    {
                        ConnectionId = Context.ConnectionId,
                        Status = UserPresenceStatus.Online,
                        LastSeenAt = DateTime.UtcNow,
                        User = currentUser,
                        IsActive = true
                    }
                });

                _logger.LogInformation(
                    "User {UserId} ({Username}) joined workspace {WorkspaceId} in shard {Shard}", 
                    userId, currentUser.Username, workspaceId, shardGroup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining workspace {WorkspaceId} for user {UserId}", 
                    workspaceId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro interno ao entrar no workspace",
                    Code = "WORKSPACE_JOIN_ERROR"
                });
            }
        }

        /// <summary>
        /// Usuário sai de um workspace
        /// </summary>
        public async Task LeaveWorkspace(string workspaceId)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                var shardGroup = GetShardGroup(workspaceId);
                
                // Remover do grupo
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, shardGroup);

                // Atualizar presença para offline
                await _presenceService.SetUserPresenceAsync(
                    workspaceGuid, 
                    userId, 
                    Context.ConnectionId, 
                    UserPresenceStatus.Offline);

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.LeaveWorkspace, "workspace", workspaceId, userId);

                // Métricas
                await _metricsService.IncrementAsync("workspace_leaves", 1, new { 
                    workspaceId,
                    shard = shardGroup
                });

                // Obter dados do usuário
                var currentUser = await GetCurrentUserDto(userId);

                // Notificar outros usuários
                await Clients.OthersInGroup(shardGroup).SendAsync("UserLeft", new
                {
                    WorkspaceId = workspaceId,
                    UserId = userId.ToString(),
                    User = currentUser,
                    LeftAt = DateTime.UtcNow
                });

                // Confirmar saída para o usuário
                await Clients.Caller.SendAsync("WorkspaceLeft", new
                {
                    WorkspaceId = workspaceId,
                    LeftAt = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} left workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving workspace {WorkspaceId} for user {UserId}", 
                    workspaceId, userId);
            }
        }

        /// <summary>
        /// Obter lista de usuários presentes no workspace
        /// </summary>
        public async Task GetWorkspacePresence(string workspaceId)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Verificar acesso
                if (!await HasWorkspaceAccess(workspaceGuid, userId))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Acesso negado ao workspace",
                        Code = "WORKSPACE_ACCESS_DENIED"
                    });
                    return;
                }

                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "presence_query"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para consulta de presença",
                        Code = "RATE_LIMIT_PRESENCE"
                    });
                    return;
                }

                // Obter presença atual
                var presence = await _presenceService.GetWorkspacePresenceAsync(workspaceGuid);

                // Enviar resposta
                await Clients.Caller.SendAsync("WorkspacePresence", new
                {
                    WorkspaceId = workspaceId,
                    ActiveUsers = presence,
                    TotalCount = presence.Count,
                    UpdatedAt = DateTime.UtcNow
                });

                // Métricas
                await _metricsService.IncrementAsync("presence_queries", 1, new { workspaceId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presence for workspace {WorkspaceId}", workspaceId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao obter presença do workspace",
                    Code = "PRESENCE_QUERY_ERROR"
                });
            }
        }

        /// <summary>
        /// Atualizar status de presença do usuário
        /// </summary>
        public async Task UpdatePresenceStatus(string workspaceId, UserPresenceStatus status)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckPresenceLimitAsync(userId))
                {
                    return; // Silently ignore if rate limited para presence updates
                }

                // Atualizar status
                await _presenceService.SetUserPresenceAsync(
                    workspaceGuid, 
                    userId, 
                    Context.ConnectionId, 
                    status);

                // Obter dados do usuário
                var currentUser = await GetCurrentUserDto(userId);

                // Broadcast para outros usuários
                var shardGroup = GetShardGroup(workspaceId);
                await Clients.OthersInGroup(shardGroup).SendAsync("PresenceStatusChanged", new
                {
                    WorkspaceId = workspaceId,
                    UserId = userId.ToString(),
                    Status = status,
                    User = currentUser,
                    UpdatedAt = DateTime.UtcNow
                });

                // Métricas
                await _metricsService.IncrementAsync("presence_updates", 1, new { 
                    workspaceId,
                    status = status.ToString()
                });

                _logger.LogDebug("User {UserId} updated presence status to {Status} in workspace {WorkspaceId}", 
                    userId, status, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating presence status for user {UserId} in workspace {WorkspaceId}", 
                    userId, workspaceId);
            }
        }

        /// <summary>
        /// Heartbeat para manter conexão ativa
        /// </summary>
        public async Task Heartbeat(string workspaceId)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Rate limiting leve para heartbeat
                if (!await _rateLimitingService.CheckPresenceLimitAsync(userId))
                {
                    return; // Silently ignore
                }

                // Atualizar timestamp de último heartbeat
                var presence = await _context.UserPresences
                    .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceGuid && 
                                             p.UserId == userId &&
                                             p.ConnectionId == Context.ConnectionId);

                if (presence != null)
                {
                    presence.LastHeartbeat = DateTime.UtcNow;
                    presence.LastSeenAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Responder com timestamp do servidor
                await Clients.Caller.SendAsync("HeartbeatAck", new
                {
                    ServerTime = DateTime.UtcNow,
                    ConnectionId = Context.ConnectionId
                });

                // Métricas (sampling para evitar spam)
                if (DateTime.UtcNow.Second % 30 == 0) // Uma vez a cada 30 segundos
                {
                    await _metricsService.IncrementAsync("heartbeats", 1, new { workspaceId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing heartbeat for user {UserId} in workspace {WorkspaceId}", 
                    userId, workspaceId);
            }
        }

        /// <summary>
        /// Eventos do ciclo de vida da conexão - Conectar
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            
            try
            {
                // Métricas de conexão
                await _metricsService.IncrementAsync("hub_connections", 1);
                await _metricsService.SetGaugeAsync("active_connections", 
                    await GetActiveConnectionCount());

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.Connect, "hub", Context.ConnectionId, userId,
                    JsonSerializer.Serialize(new {
                        userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString(),
                        ipAddress = GetClientIpAddress()
                    }));
                
                _logger.LogInformation(
                    "User {UserId} connected with connection {ConnectionId} from {IpAddress}", 
                    userId, Context.ConnectionId, GetClientIpAddress());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection setup for user {UserId}", userId);
            }
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Eventos do ciclo de vida da conexão - Desconectar
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = GetUserId();
            
            try
            {
                // Atualizar todas as presenças para offline
                await _presenceService.SetUserOfflineAsync(Context.ConnectionId);

                // Notificar desconexão em todos os workspaces onde o usuário estava
                var userWorkspaces = await _context.UserPresences
                    .Where(p => p.ConnectionId == Context.ConnectionId)
                    .Select(p => p.WorkspaceId)
                    .ToListAsync();

                var currentUser = await GetCurrentUserDto(userId);

                foreach (var workspaceId in userWorkspaces)
                {
                    var shardGroup = GetShardGroup(workspaceId.ToString());
                    
                    await _hubContext.Clients.Group(shardGroup).SendAsync("UserDisconnected", new
                    {
                        WorkspaceId = workspaceId.ToString(),
                        UserId = userId.ToString(),
                        User = currentUser,
                        DisconnectedAt = DateTime.UtcNow,
                        Reason = exception?.Message
                    });
                }

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.Disconnect, "hub", Context.ConnectionId, userId,
                    JsonSerializer.Serialize(new {
                        reason = exception?.Message,
                        workspacesAffected = userWorkspaces.Count
                    }));

                // Métricas
                await _metricsService.IncrementAsync("hub_disconnections", 1);
                await _metricsService.SetGaugeAsync("active_connections", 
                    await GetActiveConnectionCount() - 1);

                if (exception != null)
                {
                    await _metricsService.IncrementAsync("hub_disconnections_with_error", 1);
                }

                _logger.LogInformation(
                    "User {UserId} disconnected with connection {ConnectionId}. Reason: {Reason}", 
                    userId, Context.ConnectionId, exception?.Message ?? "Normal");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disconnection cleanup for user {UserId}", userId);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        // ===== MÉTODOS AUXILIARES =====

        /// <summary>
        /// Obtém o ID do usuário atual do contexto JWT
        /// </summary>
        private Guid GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst("id")?.Value ??
                             Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            
            return Guid.Parse(userIdClaim);
        }

        /// <summary>
        /// Obtém dados completos do usuário atual
        /// </summary>
        private async Task<UserDto> GetCurrentUserDto(Guid userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found");
            }

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Avatar = user.Avatar,
                Plan = user.Plan
            };
        }

        /// <summary>
        /// Verifica se o usuário tem acesso ao workspace
        /// </summary>
        private async Task<bool> HasWorkspaceAccess(Guid workspaceId, Guid userId, 
            PermissionLevel minimumLevel = PermissionLevel.Reader)
        {
            var permission = await _context.WorkspacePermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.UserId == userId);

            return permission != null && permission.Level >= minimumLevel;
        }

        /// <summary>
        /// Gera grupo de shard para load balancing
        /// </summary>
        private string GetShardGroup(string workspaceId)
        {
            var shardCount = GetShardCount();
            var hash = workspaceId.GetHashCode();
            var shardIndex = Math.Abs(hash) % shardCount;
            
            return $"workspace_{workspaceId}_shard_{shardIndex}";
        }

        /// <summary>
        /// Obtém número de shards configurado
        /// </summary>
        private int GetShardCount()
        {
            var shardCountEnv = Environment.GetEnvironmentVariable("SIGNALR_SHARD_COUNT");
            return int.TryParse(shardCountEnv, out var count) ? Math.Max(1, count) : 1;
        }

        /// <summary>
        /// Obtém IP do cliente
        /// </summary>
        private string GetClientIpAddress()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext == null) return "unknown";

            var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            }
            
            return ipAddress ?? "unknown";
        }

        /// <summary>
        /// Obtém contagem de conexões ativas (aproximada)
        /// </summary>
        private async Task<int> GetActiveConnectionCount()
        {
            return await _context.UserPresences
                .CountAsync(p => p.Status != UserPresenceStatus.Offline &&
                               p.LastSeenAt > DateTime.UtcNow.AddMinutes(-5));
        }
    }
}
```

## Entregáveis da Parte 3.4

✅ **Estrutura base do WorkspaceHub** com autenticação  
✅ **JoinWorkspace/LeaveWorkspace** com validações completas  
✅ **Gestão de grupos** com sharding para load balancing  
✅ **Sistema de presença** integrado  
✅ **Rate limiting** em todas as operações  
✅ **Auditoria completa** de conexões e ações  
✅ **Métricas em tempo real** para monitoramento  
✅ **Heartbeat** para manter conexões vivas  
✅ **Tratamento de erros** robusto  

## Próximos Passos

Na **Parte 3.5**, implementaremos:
- JoinItem/LeaveItem para edição colaborativa
- SendEdit com Operational Transform
- Gestão de conflitos em tempo real
- Cursores colaborativos

**Dependência**: Esta parte (3.4) deve estar implementada e testada antes de prosseguir.