# Fase 4 - Parte 3: User Presence & Chat System

## Contexto da Implementação

Esta é a **terceira parte da Fase 4** focada na **implementação completa de presença de usuários e sistema de chat** em tempo real para colaboração.

### Objetivos da Parte 3
✅ **Sistema de presença** visual com status (online/away/busy)  
✅ **Typing indicators** em tempo real por item  
✅ **Chat system** completo com histórico  
✅ **Message reactions** com emojis  
✅ **Message editing** com time limits  
✅ **User mentions** com autocomplete  
✅ **Cache strategy** otimizada para chat  

### Pré-requisitos
- Parte 1 (Frontend Service Integration) implementada
- Parte 2 (Real-time Collaboration Core) funcionando
- Cache Redis configurado e operacional

---

## 2.2 Presença de Usuários e Awareness

Sistema de presença visual e indicadores de atividade para colaboradores. Utiliza as interfaces já definidas no arquivo 04-01.

### IDE.Infrastructure/Services/Realtime/UserPresenceService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using IDE.Application.Common.Interfaces;
using IDE.Application.Realtime.Services;
using IDE.Application.Realtime.Models;
using IDE.Infrastructure.Data;
using IDE.API.Hubs;

namespace IDE.Infrastructure.Services.Realtime
{
    public class UserPresenceService : IUserPresenceService
    {
        private readonly IRedisCacheService _cache;
        private readonly ICacheInvalidationService _cacheInvalidation;
        private readonly IHubContext<WorkspaceHub> _hubContext;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserPresenceService> _logger;

        public UserPresenceService(
            IRedisCacheService cache,
            ICacheInvalidationService cacheInvalidation,
            IHubContext<WorkspaceHub> hubContext,
            ApplicationDbContext context,
            ILogger<UserPresenceService> logger)
        {
            _cache = cache;
            _cacheInvalidation = cacheInvalidation;
            _hubContext = hubContext;
            _context = context;
            _logger = logger;
        }

    public async Task<UserPresence> UpdatePresenceAsync(Guid workspaceId, Guid userId, string connectionId, string status = "online")
        {
            var presence = new UserPresence
            {
                UserId = userId,
                WorkspaceId = workspaceId,
                ConnectionId = connectionId,
            Status = Enum.Parse<UserPresenceStatus>(status, true),
            LastSeenAt = DateTime.UtcNow
        };

        // Cache presence by connection ID for quick lookup
        await _cache.SetAsync($"presence:connection:{connectionId}", presence, TimeSpan.FromHours(1));
        
        // Cache presence by user in workspace
        await _cache.SetAsync($"presence:user:{workspaceId}:{userId}", presence, TimeSpan.FromHours(1));

        // Get user info for notification
        var user = await _context.Users.FindAsync(userId);
        
        // Notify workspace about presence update
        await _hubContext.Clients.Group($"workspace:{workspaceId}")
            .SendAsync("UserPresenceUpdated", new
            {
                UserId = userId,
                Username = user?.Username ?? "Unknown",
                Status = status,
                LastSeen = presence.LastSeenAt,
                ConnectionId = connectionId
            });

        _logger.LogDebug("User presence updated: {UserId} in workspace {WorkspaceId} - Status: {Status}",
            userId, workspaceId, status);

        return presence;
    }

    public async Task<List<UserPresence>> GetWorkspacePresenceAsync(Guid workspaceId)
    {
        var presenceList = new List<UserPresence>();
        
        // Get all cached presence for workspace
        var pattern = $"presence:user:{workspaceId}:*";
        // Note: In production, consider using Redis SCAN instead of KEYS
        // This is simplified for demonstration
        
        var cacheKeys = await _cache.GetKeysAsync(pattern);
        
        foreach (var key in cacheKeys)
        {
            var presence = await _cache.GetAsync<UserPresence>(key);
            if (presence != null && presence.LastSeenAt > DateTime.UtcNow.AddMinutes(-5))
            {
                presenceList.Add(presence);
            }
        }

        return presenceList;
    }

    public async Task UpdateTypingStatusAsync(Guid workspaceId, Guid userId, bool isTyping, string itemId = null)
    {
        var typingKey = $"typing:{workspaceId}:{userId}";
        
        if (isTyping)
        {
            var typingIndicator = new TypingIndicator
            {
                WorkspaceId = workspaceId,
                UserId = userId,
                ItemId = itemId,
                StartedAt = DateTime.UtcNow
            };
            
            await _cache.SetAsync(typingKey, typingIndicator, TimeSpan.FromSeconds(30));
        }
        else
        {
            await _cache.RemoveAsync(typingKey);
        }

        // Get user info
        var user = await _context.Users.FindAsync(userId);

        // Notify workspace about typing status
        await _hubContext.Clients.Group($"workspace:{workspaceId}")
            .SendAsync("TypingStatusChanged", new
            {
                UserId = userId,
                Username = user?.Username ?? "Unknown",
                IsTyping = isTyping,
                ItemId = itemId,
                Timestamp = DateTime.UtcNow
            });

        _logger.LogDebug("Typing status updated: {UserId} in workspace {WorkspaceId} - IsTyping: {IsTyping}",
            userId, workspaceId, isTyping);
    }

    public async Task<List<TypingIndicator>> GetTypingUsersAsync(Guid workspaceId, string itemId = null)
    {
        var typingUsers = new List<TypingIndicator>();
        var pattern = $"typing:{workspaceId}:*";
        
        var cacheKeys = await _cache.GetKeysAsync(pattern);
        
        foreach (var key in cacheKeys)
        {
            var indicator = await _cache.GetAsync<TypingIndicator>(key);
            if (indicator != null && 
                (itemId == null || indicator.ItemId == itemId) &&
                indicator.StartedAt > DateTime.UtcNow.AddSeconds(-30))
            {
                typingUsers.Add(indicator);
            }
        }

        return typingUsers;
    }

    public async Task RemovePresenceAsync(string connectionId)
    {
        var presence = await _cache.GetAsync<UserPresence>($"presence:connection:{connectionId}");
        
        if (presence != null)
        {
            await _cache.RemoveAsync($"presence:connection:{connectionId}");
            await _cache.RemoveAsync($"presence:user:{presence.WorkspaceId}:{presence.UserId}");
            
            // Remove typing indicators
            await _cache.RemoveAsync($"typing:{presence.WorkspaceId}:{presence.UserId}");

            // Notify workspace
            await _hubContext.Clients.Group($"workspace:{presence.WorkspaceId}")
                .SendAsync("UserDisconnected", new
                {
                    UserId = presence.UserId,
                    ConnectionId = connectionId,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogDebug("User presence removed: {UserId} from workspace {WorkspaceId}",
                presence.UserId, presence.WorkspaceId);
        }
    }

    public async Task CleanupStaleConnectionsAsync()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
        var pattern = "presence:connection:*";
        
        var cacheKeys = await _cache.GetKeysAsync(pattern);
        var removedCount = 0;
        
        foreach (var key in cacheKeys)
        {
            var presence = await _cache.GetAsync<UserPresence>(key);
            if (presence != null && presence.LastSeenAt < cutoffTime)
            {
                await RemovePresenceAsync(presence.ConnectionId);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} stale connections", removedCount);
        }
    }
}
```

### Domain Models para Presença

#### IDE.Domain/ValueObjects/UserPresence.cs
```csharp
public class UserPresence
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string ConnectionId { get; set; }
    public UserPresenceStatus Status { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string CurrentItem { get; set; }
}

public enum UserPresenceStatus
{
    Online,
    Away,
    Busy,
    Offline
}

public class TypingIndicator
{
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string ItemId { get; set; }
    public DateTime StartedAt { get; set; }
}
```

---

## 2.3 Sistema de Chat em Tempo Real

Implementação completa de chat com histórico, menções e reações.

### IDE.Application/Realtime/IChatService.cs
```csharp
public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(Guid workspaceId, Guid userId, string content, Guid? parentMessageId = null);
    Task<List<ChatMessage>> GetChatHistoryAsync(Guid workspaceId, int page = 1, int pageSize = 50);
    Task<ChatMessage> EditMessageAsync(Guid messageId, Guid userId, string newContent);
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
    Task<ChatMessage> AddReactionAsync(Guid messageId, Guid userId, string emoji);
    Task<bool> RemoveReactionAsync(Guid messageId, Guid userId, string emoji);
    Task<List<User>> GetMentionableUsersAsync(Guid workspaceId);
}

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private readonly IRedisCacheService _cache;
    private readonly ICacheInvalidationService _cacheInvalidationService;
    private readonly IHubContext<WorkspaceHub> _hubContext;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context,
        IRedisCacheService cache,
        ICacheInvalidationService cacheInvalidationService,
        IHubContext<WorkspaceHub> hubContext,
        IInputSanitizer inputSanitizer,
        ILogger<ChatService> logger)
    {
        _context = context;
        _cache = cache;
        _cacheInvalidationService = cacheInvalidationService;
        _hubContext = hubContext;
        _inputSanitizer = inputSanitizer;
        _logger = logger;
    }

    public async Task<ChatMessage> SendMessageAsync(Guid workspaceId, Guid userId, string content, Guid? parentMessageId = null)
    {
        // Sanitize message content
        var sanitizedContent = _inputSanitizer.SanitizeHtml(content);
        
        if (string.IsNullOrWhiteSpace(sanitizedContent))
        {
            throw new ArgumentException("Message content cannot be empty");
        }

        // Check if user has access to workspace
        var hasAccess = await _context.WorkspacePermissions
            .AnyAsync(p => p.WorkspaceId == workspaceId && p.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this workspace");
        }

        // Validate parent message if provided
        if (parentMessageId.HasValue)
        {
            var parentExists = await _context.ChatMessages
                .AnyAsync(m => m.Id == parentMessageId.Value && m.WorkspaceId == workspaceId);
            
            if (!parentExists)
            {
                throw new ArgumentException("Parent message not found");
            }
        }

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            Content = sanitizedContent,
            Type = ChatMessageType.Text,
            CreatedAt = DateTime.UtcNow,
            WorkspaceId = workspaceId,
            UserId = userId,
            ParentMessageId = parentMessageId
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // Cache recent messages
        var cacheKey = $"chat:{workspaceId}:recent";
        var recentMessages = await _cache.GetAsync<List<ChatMessage>>(cacheKey) ?? new List<ChatMessage>();
        recentMessages.Add(message);
        
        // Keep only last 100 messages in cache
        if (recentMessages.Count > 100)
        {
            recentMessages = recentMessages.TakeLast(100).ToList();
        }
        
        await _cache.SetAsync(cacheKey, recentMessages, TimeSpan.FromHours(1));

        // Get user info for notification
        var user = await _context.Users.FindAsync(userId);

        // Notify workspace members
        await _hubContext.Clients.Group($"workspace:{workspaceId}")
            .SendAsync("ChatMessage", new
            {
                Id = message.Id,
                Content = message.Content,
                UserId = userId,
                Username = user?.Username ?? "Unknown",
                UserAvatar = user?.AvatarUrl,
                Timestamp = message.CreatedAt,
                WorkspaceId = workspaceId,
                ParentMessageId = parentMessageId,
                Type = message.Type.ToString()
            });

        _logger.LogInformation("Chat message sent: {MessageId} by user {UserId} in workspace {WorkspaceId}",
            message.Id, userId, workspaceId);

        return message;
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(Guid workspaceId, int page = 1, int pageSize = 50)
    {
        // Try to get from cache first (for recent messages)
        if (page == 1)
        {
            var cacheKey = $"chat:{workspaceId}:recent";
            var cachedMessages = await _cache.GetAsync<List<ChatMessage>>(cacheKey);
            
            if (cachedMessages != null && cachedMessages.Count >= pageSize)
            {
                return cachedMessages.TakeLast(pageSize).ToList();
            }
        }

        // Get from database
        var messages = await _context.ChatMessages
            .Where(m => m.WorkspaceId == workspaceId)
            .Include(m => m.User)
            .Include(m => m.ParentMessage)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return messages.OrderBy(m => m.CreatedAt).ToList();
    }

    public async Task<ChatMessage> EditMessageAsync(Guid messageId, Guid userId, string newContent)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId);

        if (message == null)
        {
            throw new UnauthorizedAccessException("Message not found or user not authorized");
        }

        // Check if message is not too old (allow editing within 24 hours)
        if (message.CreatedAt < DateTime.UtcNow.AddHours(-24))
        {
            throw new InvalidOperationException("Message is too old to edit");
        }

        var sanitizedContent = _inputSanitizer.SanitizeHtml(newContent);
        
        message.Content = sanitizedContent;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _cacheInvalidationService.InvalidatePatternAsync($"chat:{message.WorkspaceId}:*");

        // Notify workspace members
        await _hubContext.Clients.Group($"workspace:{message.WorkspaceId}")
            .SendAsync("ChatMessageEdited", new
            {
                Id = message.Id,
                Content = message.Content,
                EditedAt = message.EditedAt,
                Timestamp = DateTime.UtcNow
            });

        _logger.LogInformation("Chat message edited: {MessageId} by user {UserId}", messageId, userId);

        return message;
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId);

        if (message == null)
        {
            return false;
        }

        // Soft delete (mark as deleted)
        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _cacheInvalidationService.InvalidatePatternAsync($"chat:{message.WorkspaceId}:*");

        // Notify workspace members
        await _hubContext.Clients.Group($"workspace:{message.WorkspaceId}")
            .SendAsync("ChatMessageDeleted", new
            {
                Id = messageId,
                Timestamp = DateTime.UtcNow
            });

        _logger.LogInformation("Chat message deleted: {MessageId} by user {UserId}", messageId, userId);

        return true;
    }

    public async Task<ChatMessage> AddReactionAsync(Guid messageId, Guid userId, string emoji)
    {
        var message = await _context.ChatMessages
            .Include(m => m.Reactions)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            throw new ArgumentException("Message not found");
        }

        // Check if user already reacted with this emoji
        var existingReaction = message.Reactions
            .FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji);

        if (existingReaction != null)
        {
            return message; // Already reacted
        }

        var reaction = new ChatReaction
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            UserId = userId,
            Emoji = emoji,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatReactions.Add(reaction);
        await _context.SaveChangesAsync();

        // Notify workspace members
        await _hubContext.Clients.Group($"workspace:{message.WorkspaceId}")
            .SendAsync("ChatReactionAdded", new
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                Timestamp = reaction.CreatedAt
            });

        return message;
    }

    public async Task<bool> RemoveReactionAsync(Guid messageId, Guid userId, string emoji)
    {
        var reaction = await _context.ChatReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

        if (reaction == null)
        {
            return false;
        }

        _context.ChatReactions.Remove(reaction);
        await _context.SaveChangesAsync();

        // Get workspace ID for notification
        var message = await _context.ChatMessages.FindAsync(messageId);

        // Notify workspace members
        await _hubContext.Clients.Group($"workspace:{message.WorkspaceId}")
            .SendAsync("ChatReactionRemoved", new
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                Timestamp = DateTime.UtcNow
            });

        return true;
    }

    public async Task<List<User>> GetMentionableUsersAsync(Guid workspaceId)
    {
        var cacheKey = $"workspace:{workspaceId}:members";
        var cachedUsers = await _cache.GetAsync<List<User>>(cacheKey);
        
        if (cachedUsers != null)
        {
            return cachedUsers;
        }

        var users = await _context.WorkspacePermissions
            .Where(p => p.WorkspaceId == workspaceId)
            .Include(p => p.User)
            .Select(p => new User
            {
                Id = p.User.Id,
                Username = p.User.Username,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                AvatarUrl = p.User.AvatarUrl
            })
            .ToListAsync();

        await _cache.SetAsync(cacheKey, users, TimeSpan.FromMinutes(30));

        return users;
    }
}
```

### Domain Models para Chat

#### IDE.Domain/Entities/ChatMessage.cs
```csharp
public class ChatMessage
{
    public Guid Id { get; set; }
    public string Content { get; set; }
    public ChatMessageType Type { get; set; } = ChatMessageType.Text;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsEdited { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ParentMessageId { get; set; } // For threading

    // Navigation properties
    public virtual Workspace Workspace { get; set; }
    public virtual User User { get; set; }
    public virtual ChatMessage ParentMessage { get; set; }
    public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
    public virtual ICollection<ChatReaction> Reactions { get; set; } = new List<ChatReaction>();
}

public enum ChatMessageType
{
    Text,
    File,
    Image,
    Code,
    System
}
```

#### IDE.Domain/Entities/ChatReaction.cs
```csharp
public class ChatReaction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string Emoji { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual ChatMessage Message { get; set; }
    public virtual User User { get; set; }
}
```

### SignalR Hub Extensions para Presença e Chat

#### IDE.Infrastructure/SignalR/WorkspaceHub.cs (Extensão Chat)
```csharp
public partial class WorkspaceHub : Hub
{
    private readonly IUserPresenceService _userPresenceService;
    private readonly IChatService _chatService;

    // Additional methods for presence and chat

    public async Task UpdatePresence(string workspaceId, string status = "online")
    {
        if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            return;

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return;

        await _userPresenceService.UpdatePresenceAsync(
            workspaceGuid, userId, Context.ConnectionId, status);
    }

    public async Task StartTyping(string workspaceId, string itemId = null)
    {
        if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            return;

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return;

        await _userPresenceService.UpdateTypingStatusAsync(
            workspaceGuid, userId, true, itemId);
    }

    public async Task StopTyping(string workspaceId, string itemId = null)
    {
        if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            return;

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return;

        await _userPresenceService.UpdateTypingStatusAsync(
            workspaceGuid, userId, false, itemId);
    }

    public async Task SendChatMessage(string workspaceId, string content, string parentMessageId = null)
    {
        if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            return;

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return;

        Guid? parentId = null;
        if (!string.IsNullOrEmpty(parentMessageId) && Guid.TryParse(parentMessageId, out var parentGuid))
        {
            parentId = parentGuid;
        }

        try
        {
            var message = await _chatService.SendMessageAsync(workspaceGuid, userId, content, parentId);
            
            // Message is automatically sent via SignalR in the service
            await Clients.Caller.SendAsync("MessageSent", new
            {
                Success = true,
                MessageId = message.Id,
                Timestamp = message.CreatedAt
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("MessageSent", new
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    public async Task AddReaction(string messageId, string emoji)
    {
        if (!Guid.TryParse(messageId, out var messageGuid))
            return;

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return;

        try
        {
            await _chatService.AddReactionAsync(messageGuid, userId, emoji);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReactionError", new
            {
                MessageId = messageId,
                Error = ex.Message
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        await _userPresenceService.RemovePresenceAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

---

## Entregáveis da Parte 3

### ✅ Implementações Completas
- **IUserPresenceService** com status e typing indicators
- **IChatService** completo com reactions e mentions
- **UserPresence** e **TypingIndicator** value objects
- **ChatMessage** e **ChatReaction** domain models
- **WorkspaceHub** extensões para presença e chat

### ✅ Funcionalidades de Presença
- **Status tracking** (online/away/busy/offline)
- **Connection management** com cleanup automático
- **Typing indicators** com timeout de 30 segundos
- **Real-time notifications** via SignalR
- **Stale connection** cleanup automático

### ✅ Funcionalidades de Chat
- **Message sending** com sanitização automática
- **Message editing** com time limit de 24h
- **Message deletion** (soft delete)
- **Emoji reactions** com add/remove
- **Message threading** com parent/replies
- **User mentions** com autocomplete
- **Chat history** com pagination e cache

### ✅ Cache Strategy
- **Recent messages** cached por 1 hora
- **User presence** cached por 1 hora
- **Typing indicators** TTL de 30 segundos
- **Workspace members** cached por 30 minutos
- **Pattern-based** cache invalidation

---

## Validação da Parte 3

### Critérios de Sucesso
- [ ] User presence updates em tempo real
- [ ] Typing indicators aparecem e somem corretamente
- [ ] Chat messages são entregues instantaneamente
- [ ] Message reactions funcionam bidirecionalmente
- [ ] Message editing mantém histórico
- [ ] User mentions têm autocomplete funcional
- [ ] Cache strategy mantém performance adequada
- [ ] Cleanup de conexões stale funciona

### Testes de Presença
```bash
# 1. Testar update de presença
curl -X POST http://localhost:8503/api/presence/update \
  -H "Authorization: Bearer <token>" \
  -d '{"workspaceId":"<workspace-id>","status":"online"}'

# 2. Verificar usuários online
curl -X GET http://localhost:8503/api/presence/workspace/<workspace-id> \
  -H "Authorization: Bearer <token>"
```

### Testes de Chat
```bash
# 1. Enviar mensagem
curl -X POST http://localhost:8503/api/chat/message \
  -H "Authorization: Bearer <token>" \
  -d '{"workspaceId":"<workspace-id>","content":"Hello World!"}'

# 2. Buscar histórico
curl -X GET http://localhost:8503/api/chat/history/<workspace-id>?page=1&size=20 \
  -H "Authorization: Bearer <token>"

# 3. Adicionar reação
curl -X POST http://localhost:8503/api/chat/reaction \
  -H "Authorization: Bearer <token>" \
  -d '{"messageId":"<message-id>","emoji":"👍"}'
```

---

## Performance Targets

### Métricas de Presença
- **Presence updates**: < 100ms latency
- **Typing indicators**: < 50ms latency  
- **Connection cleanup**: 10min intervals
- **Cache hit rate**: > 90% para presence

### Métricas de Chat
- **Message delivery**: < 200ms latency
- **Chat history**: < 300ms load time
- **Cache hit rate**: > 85% para recent messages
- **Concurrent users**: 50+ por workspace

---

## Próximos Passos

Após validação da Parte 3, prosseguir para:
- **Parte 4**: Cache Strategy Implementation
- **Parte 5**: Database & API Optimization

---

**Tempo Estimado**: 4-5 horas  
**Complexidade**: Média-Alta  
**Dependências**: Partes 1-2, Cache Redis, SignalR Hub  
**Entregável**: Sistema completo de presença e chat funcionando