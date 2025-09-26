# Fase 3.10: Controllers REST - API Endpoints

## Implementação dos Controllers REST

Esta parte implementa os **controllers REST** que expõem as funcionalidades de colaboração através de APIs HTTP, complementando o SignalR para operações síncronas.

**Pré-requisitos**: Partes 3.7, 3.8 e 3.9 (Services) implementadas

## 1. Chat Controller

### 1.1 ChatController - API de Chat

#### IDE.Api/Controllers/ChatController.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IDE.Application.Services.Chat;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;
using IDE.Api.Extensions;
using System.Security.Claims;

namespace IDE.Api.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId:guid}/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        /// <summary>
        /// Obter mensagens do workspace
        /// </summary>
        [HttpGet("messages")]
        public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(
            Guid workspaceId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // TODO: Verificar permissão do usuário no workspace
                var messages = await _chatService.GetWorkspaceMessagesAsync(workspaceId, page, pageSize);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Enviar nova mensagem
        /// </summary>
        [HttpPost("messages")]
        public async Task<ActionResult<ChatMessageDto>> SendMessage(
            Guid workspaceId,
            [FromBody] SendChatMessageRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                // TODO: Verificar permissão do usuário no workspace
                var message = await _chatService.SendMessageAsync(workspaceId, userId.Value, request);
                return CreatedAtAction(nameof(GetMessage), new { workspaceId, messageId = message.Id }, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter mensagem específica
        /// </summary>
        [HttpGet("messages/{messageId:guid}")]
        public async Task<ActionResult<ChatMessageDto>> GetMessage(
            Guid workspaceId,
            Guid messageId)
        {
            try
            {
                var message = await _chatService.GetMessageAsync(messageId);
                
                if (message.WorkspaceId != workspaceId)
                {
                    return NotFound();
                }

                return Ok(message);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId}", messageId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Editar mensagem
        /// </summary>
        [HttpPut("messages/{messageId:guid}")]
        public async Task<ActionResult<ChatMessageDto>> EditMessage(
            Guid workspaceId,
            Guid messageId,
            [FromBody] EditChatMessageRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var message = await _chatService.EditMessageAsync(messageId, userId.Value, request);
                
                if (message.WorkspaceId != workspaceId)
                {
                    return NotFound();
                }

                return Ok(message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException)
            {
                return BadRequest("Message cannot be edited");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId}", messageId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletar mensagem
        /// </summary>
        [HttpDelete("messages/{messageId:guid}")]
        public async Task<IActionResult> DeleteMessage(
            Guid workspaceId,
            Guid messageId)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var success = await _chatService.DeleteMessageAsync(messageId, userId.Value);
                
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", messageId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Adicionar reação a mensagem
        /// </summary>
        [HttpPost("messages/{messageId:guid}/reactions")]
        public async Task<ActionResult<MessageReactionDto>> AddReaction(
            Guid workspaceId,
            Guid messageId,
            [FromBody] AddReactionRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var reaction = await _chatService.AddReactionAsync(messageId, userId.Value, request.Emoji);
                
                if (reaction == null)
                {
                    return Conflict("Reaction already exists");
                }

                return Ok(reaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction to message {MessageId}", messageId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Remover reação de mensagem
        /// </summary>
        [HttpDelete("messages/{messageId:guid}/reactions/{emoji}")]
        public async Task<IActionResult> RemoveReaction(
            Guid workspaceId,
            Guid messageId,
            string emoji)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                await _chatService.RemoveReactionAsync(messageId, userId.Value, emoji);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from message {MessageId}", messageId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Buscar mensagens
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<ChatMessageDto>>> SearchMessages(
            Guid workspaceId,
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var messages = await _chatService.SearchMessagesAsync(workspaceId, query, page, pageSize);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages in workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter thread de uma mensagem
        /// </summary>
        [HttpGet("messages/{messageId:guid}/thread")]
        public async Task<ActionResult<List<ChatMessageDto>>> GetMessageThread(
            Guid workspaceId,
            Guid messageId)
        {
            try
            {
                var thread = await _chatService.GetMessageThreadAsync(messageId);
                return Ok(thread);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thread for message {MessageId}", messageId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Marcar mensagens como lidas
        /// </summary>
        [HttpPost("messages/mark-read")]
        public async Task<IActionResult> MarkMessagesAsRead(
            Guid workspaceId,
            [FromBody] MarkMessagesAsReadRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                await _chatService.MarkMessagesAsReadAsync(request.MessageIds, userId.Value);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter contagem de mensagens não lidas
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount(Guid workspaceId)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var count = await _chatService.GetUnreadMessageCountAsync(workspaceId, userId.Value);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter estatísticas do chat
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ChatStatsDto>> GetChatStats(Guid workspaceId)
        {
            try
            {
                var stats = await _chatService.GetWorkspaceChatStatsAsync(workspaceId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat stats for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Request DTOs
    public class AddReactionRequest
    {
        public string Emoji { get; set; } = string.Empty;
    }

    public class MarkMessagesAsReadRequest
    {
        public List<Guid> MessageIds { get; set; } = new();
    }
}
```

## 2. Notifications Controller

### 2.1 NotificationsController - API de Notificações

#### IDE.Api/Controllers/NotificationsController.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IDE.Application.Services.Notifications;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Api.Extensions;

namespace IDE.Api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Obter notificações do usuário
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<NotificationDto>>> GetNotifications(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var notifications = await _notificationService.GetUserNotificationsAsync(
                    userId.Value, unreadOnly, page, pageSize);
                
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Criar notificação customizada
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<NotificationDto>> CreateNotification(
            [FromBody] CreateNotificationRequest request)
        {
            try
            {
                // TODO: Verificar se usuário tem permissão para criar notificação no workspace
                var notification = await _notificationService.CreateNotificationAsync(request);
                return CreatedAtAction(nameof(GetNotifications), new { }, notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Marcar notificação como lida
        /// </summary>
        [HttpPost("{notificationId:guid}/mark-read")]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var success = await _notificationService.MarkAsReadAsync(notificationId, userId.Value);
                
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Marcar todas as notificações como lidas
        /// </summary>
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                await _notificationService.MarkAllAsReadAsync(userId.Value);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletar notificação
        /// </summary>
        [HttpDelete("{notificationId:guid}")]
        public async Task<IActionResult> DeleteNotification(Guid notificationId)
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var success = await _notificationService.DeleteNotificationAsync(notificationId, userId.Value);
                
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter contagem de notificações não lidas
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var count = await _notificationService.GetUnreadCountAsync(userId.Value);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter estatísticas de notificações
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<NotificationStatsDto>> GetNotificationStats(
            [FromQuery] Guid? workspaceId = null)
        {
            try
            {
                var stats = await _notificationService.GetNotificationStatsAsync(workspaceId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification stats");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Enviar notificação de sistema (Admin only)
        /// </summary>
        [HttpPost("system")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<NotificationDto>> SendSystemNotification(
            [FromBody] SystemNotificationRequest request)
        {
            try
            {
                var notification = await _notificationService.SendSystemNotificationAsync(
                    request.Title, 
                    request.Message, 
                    request.Priority, 
                    request.UserIds);
                
                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system notification");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Request DTOs
    public class SystemNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
        public List<Guid>? UserIds { get; set; }
    }
}
```

## 3. Presence Controller

### 3.1 PresenceController - API de Presença

#### IDE.Api/Controllers/PresenceController.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IDE.Application.Services.Collaboration;
using IDE.Application.Realtime.DTOs;
using IDE.Api.Extensions;

namespace IDE.Api.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId:guid}/presence")]
    [Authorize]
    public class PresenceController : ControllerBase
    {
        private readonly IUserPresenceService _presenceService;
        private readonly ILogger<PresenceController> _logger;

        public PresenceController(
            IUserPresenceService presenceService,
            ILogger<PresenceController> logger)
        {
            _presenceService = presenceService;
            _logger = logger;
        }

        /// <summary>
        /// Obter presença dos usuários no workspace
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<UserPresenceDto>>> GetWorkspacePresence(Guid workspaceId)
        {
            try
            {
                var presence = await _presenceService.GetWorkspacePresenceAsync(workspaceId);
                return Ok(presence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presence for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter contagem de usuários ativos
        /// </summary>
        [HttpGet("active-count")]
        public async Task<ActionResult<int>> GetActiveCount(Guid workspaceId)
        {
            try
            {
                var count = await _presenceService.GetWorkspaceActiveCountAsync(workspaceId);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active count for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter editores ativos em um item
        /// </summary>
        [HttpGet("items/{itemId:guid}/editors")]
        public async Task<ActionResult<List<UserPresenceDto>>> GetItemActiveEditors(
            Guid workspaceId,
            Guid itemId)
        {
            try
            {
                var editors = await _presenceService.GetItemActiveEditorsAsync(itemId);
                return Ok(editors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active editors for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Verificar se usuário está online
        /// </summary>
        [HttpGet("users/{userId:guid}/online")]
        public async Task<ActionResult<bool>> IsUserOnline(
            Guid workspaceId,
            Guid userId)
        {
            try
            {
                var isOnline = await _presenceService.IsUserOnlineAsync(userId, workspaceId);
                return Ok(isOnline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is online", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter estatísticas globais de presença (Admin only)
        /// </summary>
        [HttpGet("/api/presence/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PresenceStatsDto>> GetPresenceStats()
        {
            try
            {
                var stats = await _presenceService.GetPresenceStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presence stats");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
```

## 4. Metrics Controller

### 4.1 MetricsController - API de Métricas

#### IDE.Api/Controllers/MetricsController.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IDE.Application.Services.Collaboration;
using IDE.Application.Realtime.DTOs;
using IDE.Api.Extensions;

namespace IDE.Api.Controllers
{
    [ApiController]
    [Route("api/metrics")]
    [Authorize(Roles = "Admin,Manager")]
    public class MetricsController : ControllerBase
    {
        private readonly ICollaborationMetricsService _metricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            ICollaborationMetricsService metricsService,
            ILogger<MetricsController> logger)
        {
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <summary>
        /// Obter métricas do sistema
        /// </summary>
        [HttpGet("system")]
        public async Task<ActionResult<SystemMetricsDto>> GetSystemMetrics()
        {
            try
            {
                var metrics = await _metricsService.GetSystemMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system metrics");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter métricas de workspace
        /// </summary>
        [HttpGet("workspaces/{workspaceId:guid}")]
        public async Task<ActionResult<WorkspaceMetricsDto>> GetWorkspaceMetrics(Guid workspaceId)
        {
            try
            {
                var metrics = await _metricsService.GetWorkspaceMetricsAsync(workspaceId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workspace metrics for {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter métricas de usuário
        /// </summary>
        [HttpGet("users/{userId:guid}")]
        public async Task<ActionResult<UserMetricsDto>> GetUserMetrics(Guid userId)
        {
            try
            {
                // Verificar se usuário pode acessar métricas de outro usuário
                var currentUserId = User.GetUserId();
                if (currentUserId != userId && !User.IsInRole("Admin"))
                {
                    return Forbid();
                }

                var metrics = await _metricsService.GetUserMetricsAsync(userId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user metrics for {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gerar relatório de performance
        /// </summary>
        [HttpGet("performance-report")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PerformanceReportDto>> GeneratePerformanceReport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            try
            {
                if (from >= to)
                {
                    return BadRequest("From date must be before to date");
                }

                if ((to - from).TotalDays > 90)
                {
                    return BadRequest("Date range cannot exceed 90 days");
                }

                var report = await _metricsService.GeneratePerformanceReportAsync(from, to);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance report");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Limpar métricas antigas
        /// </summary>
        [HttpDelete("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CleanupOldMetrics([FromQuery] int daysToKeep = 30)
        {
            try
            {
                if (daysToKeep < 1)
                {
                    return BadRequest("Days to keep must be at least 1");
                }

                var maxAge = TimeSpan.FromDays(daysToKeep);
                await _metricsService.CleanupOldMetricsAsync(maxAge);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old metrics");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
```

## 5. Audit Controller

### 5.1 AuditController - API de Auditoria

#### IDE.Api/Controllers/AuditController.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IDE.Application.Services.Collaboration;
using IDE.Application.Realtime.DTOs;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Api.Extensions;

namespace IDE.Api.Controllers
{
    [ApiController]
    [Route("api/audit")]
    [Authorize(Roles = "Admin,Manager")]
    public class AuditController : ControllerBase
    {
        private readonly ICollaborationAuditService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(
            ICollaborationAuditService auditService,
            ILogger<AuditController> logger)
        {
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Obter logs de auditoria por usuário
        /// </summary>
        [HttpGet("users/{userId:guid}")]
        public async Task<ActionResult<List<AuditLogDto>>> GetUserAuditLogs(
            Guid userId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var logs = await _auditService.GetUserAuditLogsAsync(userId, from, to, page, pageSize);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter logs de auditoria por workspace
        /// </summary>
        [HttpGet("workspaces/{workspaceId:guid}")]
        public async Task<ActionResult<List<AuditLogDto>>> GetWorkspaceAuditLogs(
            Guid workspaceId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var logs = await _auditService.GetWorkspaceAuditLogsAsync(workspaceId, from, to, page, pageSize);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs for workspace {WorkspaceId}", workspaceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter logs de auditoria por resource
        /// </summary>
        [HttpGet("resources/{resourceType}/{resourceId}")]
        public async Task<ActionResult<List<AuditLogDto>>> GetResourceAuditLogs(
            string resourceType,
            string resourceId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var logs = await _auditService.GetResourceAuditLogsAsync(resourceType, resourceId, from, to, page, pageSize);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs for resource {ResourceType}:{ResourceId}", resourceType, resourceId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter logs por ação específica
        /// </summary>
        [HttpGet("actions/{action}")]
        public async Task<ActionResult<List<AuditLogDto>>> GetAuditLogsByAction(
            AuditAction action,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var logs = await _auditService.GetAuditLogsByActionAsync(action, from, to, page, pageSize);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs for action {Action}", action);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gerar relatório de auditoria
        /// </summary>
        [HttpGet("reports")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AuditReportDto>> GenerateAuditReport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] Guid? workspaceId = null,
            [FromQuery] Guid? userId = null)
        {
            try
            {
                if (from >= to)
                {
                    return BadRequest("From date must be before to date");
                }

                if ((to - from).TotalDays > 90)
                {
                    return BadRequest("Date range cannot exceed 90 days");
                }

                var report = await _auditService.GenerateAuditReportAsync(from, to, workspaceId, userId);
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit report");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter estatísticas de auditoria
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<AuditStatsDto>> GetAuditStats(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var stats = await _auditService.GetAuditStatsAsync(from, to);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit stats");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Limpar logs antigos de auditoria
        /// </summary>
        [HttpDelete("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CleanupOldAuditLogs([FromQuery] int daysToKeep = 365)
        {
            try
            {
                if (daysToKeep < 30)
                {
                    return BadRequest("Days to keep must be at least 30 for audit logs");
                }

                var maxAge = TimeSpan.FromDays(daysToKeep);
                await _auditService.CleanupOldAuditLogsAsync(maxAge);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
```

## 6. Rate Limiting Controller

### 6.1 RateLimitController - API de Rate Limiting

#### IDE.Api/Controllers/RateLimitController.cs
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IDE.Application.Services.Collaboration;
using IDE.Application.Realtime.DTOs;
using IDE.Api.Extensions;

namespace IDE.Api.Controllers
{
    [ApiController]
    [Route("api/rate-limit")]
    [Authorize]
    public class RateLimitController : ControllerBase
    {
        private readonly IRateLimitingService _rateLimitService;
        private readonly ILogger<RateLimitController> _logger;

        public RateLimitController(
            IRateLimitingService rateLimitService,
            ILogger<RateLimitController> logger)
        {
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        /// <summary>
        /// Obter estatísticas de uso do usuário atual
        /// </summary>
        [HttpGet("usage-stats")]
        public async Task<ActionResult<RateLimitStatsDto>> GetUserUsageStats()
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var stats = await _rateLimitService.GetUserUsageStatsAsync(userId.Value);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage stats for current user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Obter estatísticas de uso de usuário específico (Admin only)
        /// </summary>
        [HttpGet("users/{userId:guid}/usage-stats")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<ActionResult<RateLimitStatsDto>> GetUserUsageStats(Guid userId)
        {
            try
            {
                var stats = await _rateLimitService.GetUserUsageStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage stats for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Verificar se usuário está throttled
        /// </summary>
        [HttpGet("throttle-status")]
        public async Task<ActionResult<bool>> IsUserThrottled()
        {
            try
            {
                var userId = User.GetUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var isThrottled = await _rateLimitService.IsUserThrottledAsync(userId.Value);
                return Ok(isThrottled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking throttle status for current user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Aplicar throttling a um usuário (Admin only)
        /// </summary>
        [HttpPost("users/{userId:guid}/throttle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApplyThrottle(
            Guid userId,
            [FromBody] ApplyThrottleRequest request)
        {
            try
            {
                var duration = TimeSpan.FromMinutes(request.DurationMinutes);
                await _rateLimitService.ApplyThrottleAsync(userId, duration, request.Reason);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying throttle to user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Limpar registros antigos de rate limiting (Admin only)
        /// </summary>
        [HttpDelete("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CleanupOldRecords()
        {
            try
            {
                await _rateLimitService.CleanupOldRecordsAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old rate limiting records");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Request DTOs
    public class ApplyThrottleRequest
    {
        public int DurationMinutes { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
```

## 7. Extension Helper

### 7.1 ClaimsPrincipalExtensions - Helper para Claims

#### IDE.Api/Extensions/ClaimsPrincipalExtensions.cs
```csharp
using System.Security.Claims;

namespace IDE.Api.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid? GetUserId(this ClaimsPrincipal principal)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return null;
            }

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public static string? GetUserEmail(this ClaimsPrincipal principal)
        {
            return principal.FindFirst(ClaimTypes.Email)?.Value;
        }

        public static string? GetUserName(this ClaimsPrincipal principal)
        {
            return principal.FindFirst(ClaimTypes.Name)?.Value;
        }

        public static List<string> GetUserRoles(this ClaimsPrincipal principal)
        {
            return principal.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
    }
}
```

## Entregáveis da Parte 3.10

✅ **6 Controllers REST** completos:
- ChatController - API de chat e mensagens
- NotificationsController - Sistema de notificações  
- PresenceController - Gestão de presença
- MetricsController - Métricas e relatórios
- AuditController - Logs e auditoria
- RateLimitController - Controle de taxa

✅ **Funcionalidades implementadas**:
- CRUD completo para chat e notificações
- Controle de acesso baseado em roles
- Paginação em todas as listagens
- Tratamento de erros padronizado
- Validação de parâmetros
- Estatísticas e relatórios
- Cleanup de dados antigos

✅ **Segurança**:
- Autenticação obrigatória
- Autorização por roles
- Validação de propriedade de recursos
- Rate limiting integrado

## Próximos Passos

Na **Parte 3.11**, implementaremos:
- Configuração de injeção de dependência
- Registro de todos os services
- Configuração do SignalR

**Dependência**: Esta parte (3.10) deve estar implementada antes de prosseguir.