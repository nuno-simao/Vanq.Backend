# Fase 3.8: Services - Chat e Notificações

## Implementação dos Services de Comunicação

Esta parte implementa os **services de chat** e **notificações** que suportam a comunicação em tempo real entre usuários do workspace.

**Pré-requisitos**: Parte 3.7 (Services de Presença) implementada

## 1. Service de Chat

### 1.1 Interface do Chat Service

#### IDE.Application/Services/Chat/IChatService.cs
```csharp
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;

namespace IDE.Application.Services.Chat
{
    /// <summary>
    /// Service para operações de chat e mensagens
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Enviar mensagem no chat do workspace
        /// </summary>
        Task<ChatMessageDto> SendMessageAsync(Guid workspaceId, Guid userId, SendChatMessageRequest request);
        
        /// <summary>
        /// Editar mensagem existente
        /// </summary>
        Task<ChatMessageDto> EditMessageAsync(Guid messageId, Guid userId, EditChatMessageRequest request);
        
        /// <summary>
        /// Obter mensagem por ID
        /// </summary>
        Task<ChatMessageDto> GetMessageAsync(Guid messageId);
        
        /// <summary>
        /// Obter histórico de mensagens do workspace
        /// </summary>
        Task<List<ChatMessageDto>> GetWorkspaceMessagesAsync(Guid workspaceId, int page = 1, int pageSize = 50);
        
        /// <summary>
        /// Adicionar reação a uma mensagem
        /// </summary>
        Task<MessageReactionDto?> AddReactionAsync(Guid messageId, Guid userId, string emoji);
        
        /// <summary>
        /// Remover reação de uma mensagem
        /// </summary>
        Task RemoveReactionAsync(Guid messageId, Guid userId, string emoji);
        
        /// <summary>
        /// Marcar mensagens como lidas por um usuário
        /// </summary>
        Task MarkMessagesAsReadAsync(List<Guid> messageIds, Guid userId);
        
        /// <summary>
        /// Obter contagem de mensagens não lidas para um usuário
        /// </summary>
        Task<int> GetUnreadMessageCountAsync(Guid workspaceId, Guid userId);
        
        /// <summary>
        /// Deletar mensagem (soft delete)
        /// </summary>
        Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
        
        /// <summary>
        /// Buscar mensagens no workspace
        /// </summary>
        Task<List<ChatMessageDto>> SearchMessagesAsync(Guid workspaceId, string query, int page = 1, int pageSize = 20);
        
        /// <summary>
        /// Obter threads de uma mensagem (respostas)
        /// </summary>
        Task<List<ChatMessageDto>> GetMessageThreadAsync(Guid parentMessageId);
        
        /// <summary>
        /// Obter estatísticas do chat do workspace
        /// </summary>
        Task<ChatStatsDto> GetWorkspaceChatStatsAsync(Guid workspaceId);
    }
}
```

### 1.2 Implementação do Chat Service

#### IDE.Infrastructure/Services/Chat/ChatService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using IDE.Application.Services.Chat;
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;
using IDE.Infrastructure.Persistence.Data;
using System.Text.Json;

namespace IDE.Infrastructure.Services.Chat
{
    /// <summary>
    /// Implementação do service de chat
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ApplicationDbContext context,
            IDistributedCache cache,
            ILogger<ChatService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ChatMessageDto> SendMessageAsync(Guid workspaceId, Guid userId, SendChatMessageRequest request)
        {
            try
            {
                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    Content = request.Content,
                    Type = request.Type,
                    ParentMessageId = request.ParentMessageId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsEdited = false,
                    IsDeleted = false,
                    Metadata = request.Metadata
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                // Invalidar cache de mensagens do workspace
                await _cache.RemoveAsync($"workspace_messages_{workspaceId}");

                _logger.LogInformation("Message {MessageId} sent by user {UserId} in workspace {WorkspaceId}", 
                    chatMessage.Id, userId, workspaceId);

                return MapToDto(chatMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message in workspace {WorkspaceId} by user {UserId}", 
                    workspaceId, userId);
                throw;
            }
        }

        public async Task<ChatMessageDto> EditMessageAsync(Guid messageId, Guid userId, EditChatMessageRequest request)
        {
            try
            {
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId && !m.IsDeleted);

                if (message == null)
                {
                    throw new UnauthorizedAccessException("Mensagem não encontrada ou sem permissão para editar");
                }

                // Verificar se a mensagem não é muito antiga (ex: 1 hora)
                if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromHours(1))
                {
                    throw new InvalidOperationException("Mensagem muito antiga para ser editada");
                }

                message.Content = request.Content;
                message.UpdatedAt = DateTime.UtcNow;
                message.IsEdited = true;

                await _context.SaveChangesAsync();

                // Invalidar cache
                await _cache.RemoveAsync($"workspace_messages_{message.WorkspaceId}");

                _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, userId);

                return MapToDto(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<ChatMessageDto> GetMessageAsync(Guid messageId)
        {
            try
            {
                var message = await _context.ChatMessages
                    .Include(m => m.Reactions)
                    .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

                if (message == null)
                {
                    throw new KeyNotFoundException($"Message {messageId} not found");
                }

                return MapToDto(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId}", messageId);
                throw;
            }
        }

        public async Task<List<ChatMessageDto>> GetWorkspaceMessagesAsync(Guid workspaceId, int page = 1, int pageSize = 50)
        {
            try
            {
                var cacheKey = $"workspace_messages_{workspaceId}_{page}_{pageSize}";
                var cachedMessages = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedMessages))
                {
                    return JsonSerializer.Deserialize<List<ChatMessageDto>>(cachedMessages) ?? new List<ChatMessageDto>();
                }

                var messages = await _context.ChatMessages
                    .Include(m => m.Reactions)
                    .Where(m => m.WorkspaceId == workspaceId && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtos = messages.Select(MapToDto).ToList();

                // Cache por 5 minutos
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dtos), cacheOptions);

                return dtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for workspace {WorkspaceId}", workspaceId);
                throw;
            }
        }

        public async Task<MessageReactionDto?> AddReactionAsync(Guid messageId, Guid userId, string emoji)
        {
            try
            {
                // Verificar se a reação já existe
                var existingReaction = await _context.MessageReactions
                    .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

                if (existingReaction != null)
                {
                    return null; // Reação já existe
                }

                var reaction = new MessageReaction
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    UserId = userId,
                    Emoji = emoji,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MessageReactions.Add(reaction);
                await _context.SaveChangesAsync();

                // Invalidar cache da mensagem
                var message = await _context.ChatMessages.FirstAsync(m => m.Id == messageId);
                await _cache.RemoveAsync($"workspace_messages_{message.WorkspaceId}");

                _logger.LogInformation("Reaction {Emoji} added to message {MessageId} by user {UserId}", 
                    emoji, messageId, userId);

                return new MessageReactionDto
                {
                    Id = reaction.Id,
                    MessageId = messageId,
                    UserId = userId,
                    Emoji = emoji,
                    CreatedAt = reaction.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction {Emoji} to message {MessageId} by user {UserId}", 
                    emoji, messageId, userId);
                throw;
            }
        }

        public async Task RemoveReactionAsync(Guid messageId, Guid userId, string emoji)
        {
            try
            {
                var reaction = await _context.MessageReactions
                    .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

                if (reaction != null)
                {
                    _context.MessageReactions.Remove(reaction);
                    await _context.SaveChangesAsync();

                    // Invalidar cache da mensagem
                    var message = await _context.ChatMessages.FirstAsync(m => m.Id == messageId);
                    await _cache.RemoveAsync($"workspace_messages_{message.WorkspaceId}");

                    _logger.LogInformation("Reaction {Emoji} removed from message {MessageId} by user {UserId}", 
                        emoji, messageId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction {Emoji} from message {MessageId} by user {UserId}", 
                    emoji, messageId, userId);
                throw;
            }
        }

        public async Task MarkMessagesAsReadAsync(List<Guid> messageIds, Guid userId)
        {
            try
            {
                // Implementar lógica de marcação como lida
                // Por simplicidade, não implementando read tracking nesta versão
                _logger.LogInformation("Messages marked as read by user {UserId}: {MessageIds}", 
                    userId, string.Join(", ", messageIds));
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetUnreadMessageCountAsync(Guid workspaceId, Guid userId)
        {
            try
            {
                // Por simplicidade, retornando 0
                // Em uma implementação completa, haveria uma tabela de read tracking
                await Task.CompletedTask;
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId} in workspace {WorkspaceId}", 
                    userId, workspaceId);
                throw;
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
        {
            try
            {
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId && !m.IsDeleted);

                if (message == null)
                {
                    return false;
                }

                // Soft delete
                message.IsDeleted = true;
                message.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Invalidar cache
                await _cache.RemoveAsync($"workspace_messages_{message.WorkspaceId}");

                _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<List<ChatMessageDto>> SearchMessagesAsync(Guid workspaceId, string query, int page = 1, int pageSize = 20)
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Include(m => m.Reactions)
                    .Where(m => m.WorkspaceId == workspaceId && 
                               !m.IsDeleted && 
                               m.Content.Contains(query))
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return messages.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages in workspace {WorkspaceId} with query '{Query}'", 
                    workspaceId, query);
                throw;
            }
        }

        public async Task<List<ChatMessageDto>> GetMessageThreadAsync(Guid parentMessageId)
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Include(m => m.Reactions)
                    .Where(m => m.ParentMessageId == parentMessageId && !m.IsDeleted)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync();

                return messages.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thread for message {ParentMessageId}", parentMessageId);
                throw;
            }
        }

        public async Task<ChatStatsDto> GetWorkspaceChatStatsAsync(Guid workspaceId)
        {
            try
            {
                var now = DateTime.UtcNow;
                var today = now.Date;

                var stats = await _context.ChatMessages
                    .Where(m => m.WorkspaceId == workspaceId && !m.IsDeleted)
                    .GroupBy(m => 1)
                    .Select(g => new
                    {
                        TotalMessages = g.Count(),
                        MessagesToday = g.Count(m => m.CreatedAt >= today),
                        LastMessageAt = g.Max(m => m.CreatedAt),
                        ReactionsCount = g.Sum(m => m.Reactions.Count)
                    })
                    .FirstOrDefaultAsync();

                var activeUsers = await _context.ChatMessages
                    .Where(m => m.WorkspaceId == workspaceId && 
                               !m.IsDeleted && 
                               m.CreatedAt >= now.AddHours(-24))
                    .Select(m => m.UserId)
                    .Distinct()
                    .CountAsync();

                var messageTypeDistribution = await _context.ChatMessages
                    .Where(m => m.WorkspaceId == workspaceId && !m.IsDeleted)
                    .GroupBy(m => m.Type)
                    .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count());

                return new ChatStatsDto
                {
                    TotalMessages = stats?.TotalMessages ?? 0,
                    UnreadMessages = 0, // Simplificado
                    ActiveUsers = activeUsers,
                    LastMessageAt = stats?.LastMessageAt,
                    MessagesToday = stats?.MessagesToday ?? 0,
                    ReactionsCount = stats?.ReactionsCount ?? 0,
                    AverageMessagesPerHour = CalculateAverageMessagesPerHour(workspaceId),
                    MessageTypeDistribution = messageTypeDistribution
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat stats for workspace {WorkspaceId}", workspaceId);
                throw;
            }
        }

        #region Helper Methods

        private ChatMessageDto MapToDto(ChatMessage message)
        {
            return new ChatMessageDto
            {
                Id = message.Id,
                WorkspaceId = message.WorkspaceId,
                UserId = message.UserId,
                Content = message.Content,
                Type = message.Type,
                ParentMessageId = message.ParentMessageId,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                IsEdited = message.IsEdited,
                Reactions = message.Reactions?.Select(r => new MessageReactionDto
                {
                    Id = r.Id,
                    MessageId = r.MessageId,
                    UserId = r.UserId,
                    Emoji = r.Emoji,
                    CreatedAt = r.CreatedAt
                }).ToList() ?? new List<MessageReactionDto>(),
                Metadata = message.Metadata
            };
        }

        private double CalculateAverageMessagesPerHour(Guid workspaceId)
        {
            // Simplificado - retorna valor fixo
            // Em implementação real, calcularia com base no histórico
            return 10.0;
        }

        #endregion
    }
}
```
using IDE.Infrastructure.Data;

namespace IDE.Infrastructure.Services
{
    /// <summary>
    /// Implementação do service de chat
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ApplicationDbContext context,
            ILogger<ChatService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ChatMessageDto> SendMessageAsync(Guid workspaceId, Guid userId, SendChatMessageRequest request)
        {
            try
            {
                var message = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    Content = request.Content,
                    Type = request.Type,
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    ParentMessageId = request.ParentMessageId,
                    AttachmentUrl = request.AttachmentUrl,
                    AttachmentType = request.AttachmentType,
                    AttachmentSize = request.AttachmentSize,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsEdited = false,
                    IsDeleted = false,
                    Metadata = request.Metadata
                };

                _context.ChatMessages.Add(message);
                await _context.SaveChangesAsync();

                var messageDto = await GetMessageDtoAsync(message.Id);

                _logger.LogInformation("Message {MessageId} sent by user {UserId} to workspace {WorkspaceId}",
                    message.Id, userId, workspaceId);

                return messageDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to workspace {WorkspaceId} by user {UserId}",
                    workspaceId, userId);
                throw;
            }
        }

        public async Task<ChatMessageDto> EditMessageAsync(Guid messageId, Guid userId, EditChatMessageRequest request)
        {
            try
            {
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId && !m.IsDeleted);

                if (message == null)
                {
                    throw new UnauthorizedAccessException("Mensagem não encontrada ou usuário sem permissão");
                }

                // Verificar se a mensagem não é muito antiga para edição (ex: 24 horas)
                if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromHours(24))
                {
                    throw new InvalidOperationException("Mensagem muito antiga para edição");
                }

                message.Content = request.Content;
                message.IsEdited = true;
                message.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var messageDto = await GetMessageDtoAsync(message.Id);

                _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, userId);

                return messageDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId} by user {UserId}", messageId, userId);
                throw;
            }
        }

        public async Task<ChatMessageDto> GetMessageAsync(Guid messageId)
        {
            try
            {
                return await GetMessageDtoAsync(messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId}", messageId);
                throw;
            }
        }

        public async Task<List<ChatMessageDto>> GetWorkspaceMessagesAsync(Guid workspaceId, int page = 1, int pageSize = 50)
        {
            try
            {
                var skip = (page - 1) * pageSize;
                
                var messages = await _context.ChatMessages
                    .Where(m => m.WorkspaceId == workspaceId && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize)
                    .Include(m => m.User)
                    .Include(m => m.Reactions)
                        .ThenInclude(r => r.User)
                    .Include(m => m.ReadReceipts)
                        .ThenInclude(r => r.User)
                    .AsNoTracking()
                    .ToListAsync();

                return messages.Select(ConvertToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for workspace {WorkspaceId}", workspaceId);
                return new List<ChatMessageDto>();
            }
        }

        public async Task<MessageReactionDto?> AddReactionAsync(Guid messageId, Guid userId, string emoji)
        {
            try
            {
                // Verificar se a reação já existe
                var existingReaction = await _context.Set<MessageReaction>()
                    .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

                if (existingReaction != null)
                {
                    return null; // Reação já existe
                }

                var reaction = new MessageReaction
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    UserId = userId,
                    Emoji = emoji,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Set<MessageReaction>().Add(reaction);
                await _context.SaveChangesAsync();

                // Obter contagem total desta reação
                var totalCount = await _context.Set<MessageReaction>()
                    .CountAsync(r => r.MessageId == messageId && r.Emoji == emoji);

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                _logger.LogDebug("User {UserId} reacted with {Emoji} to message {MessageId}",
                    userId, emoji, messageId);

                return new MessageReactionDto
                {
                    Id = reaction.Id,
                    Emoji = emoji,
                    TotalCount = totalCount,
                    User = user != null ? new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Avatar = user.Avatar
                    } : null,
                    CreatedAt = reaction.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction {Emoji} to message {MessageId} by user {UserId}",
                    emoji, messageId, userId);
                throw;
            }
        }

        public async Task RemoveReactionAsync(Guid messageId, Guid userId, string emoji)
        {
            try
            {
                var reaction = await _context.Set<MessageReaction>()
                    .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

                if (reaction != null)
                {
                    _context.Set<MessageReaction>().Remove(reaction);
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("User {UserId} removed reaction {Emoji} from message {MessageId}",
                        userId, emoji, messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction {Emoji} from message {MessageId} by user {UserId}",
                    emoji, messageId, userId);
                throw;
            }
        }

        public async Task MarkMessagesAsReadAsync(List<Guid> messageIds, Guid userId)
        {
            try
            {
                var existingReceipts = await _context.Set<MessageReadReceipt>()
                    .Where(r => messageIds.Contains(r.MessageId) && r.UserId == userId)
                    .Select(r => r.MessageId)
                    .ToListAsync();

                var newReceiptIds = messageIds.Except(existingReceipts).ToList();

                if (newReceiptIds.Any())
                {
                    var receipts = newReceiptIds.Select(messageId => new MessageReadReceipt
                    {
                        Id = Guid.NewGuid(),
                        MessageId = messageId,
                        UserId = userId,
                        ReadAt = DateTime.UtcNow
                    }).ToList();

                    _context.Set<MessageReadReceipt>().AddRange(receipts);
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("User {UserId} marked {Count} messages as read", userId, newReceiptIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetUnreadMessageCountAsync(Guid workspaceId, Guid userId)
        {
            try
            {
                var totalMessages = await _context.ChatMessages
                    .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted);

                var readMessages = await _context.Set<MessageReadReceipt>()
                    .Where(r => r.UserId == userId && 
                               _context.ChatMessages.Any(m => m.Id == r.MessageId && m.WorkspaceId == workspaceId))
                    .CountAsync();

                return totalMessages - readMessages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread message count for user {UserId} in workspace {WorkspaceId}",
                    userId, workspaceId);
                return 0;
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
        {
            try
            {
                var message = await _context.ChatMessages
                    .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId);

                if (message == null)
                {
                    return false; // Mensagem não encontrada ou sem permissão
                }

                message.IsDeleted = true;
                message.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} by user {UserId}", messageId, userId);
                return false;
            }
        }

        public async Task<List<ChatMessageDto>> SearchMessagesAsync(Guid workspaceId, string query, int page = 1, int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return new List<ChatMessageDto>();
                }

                var skip = (page - 1) * pageSize;
                
                var messages = await _context.ChatMessages
                    .Where(m => m.WorkspaceId == workspaceId && 
                               !m.IsDeleted && 
                               m.Content.Contains(query))
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize)
                    .Include(m => m.User)
                    .AsNoTracking()
                    .ToListAsync();

                return messages.Select(ConvertToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching messages in workspace {WorkspaceId} with query '{Query}'",
                    workspaceId, query);
                return new List<ChatMessageDto>();
            }
        }

        public async Task<List<ChatMessageDto>> GetMessageThreadAsync(Guid parentMessageId)
        {
            try
            {
                var messages = await _context.ChatMessages
                    .Where(m => m.ParentMessageId == parentMessageId && !m.IsDeleted)
                    .OrderBy(m => m.CreatedAt)
                    .Include(m => m.User)
                    .AsNoTracking()
                    .ToListAsync();

                return messages.Select(ConvertToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thread for message {ParentMessageId}", parentMessageId);
                return new List<ChatMessageDto>();
            }
        }

        public async Task<ChatStatsDto> GetWorkspaceChatStatsAsync(Guid workspaceId)
        {
            try
            {
                var now = DateTime.UtcNow;
                var oneDayAgo = now.AddDays(-1);
                var oneWeekAgo = now.AddDays(-7);
                var oneMonthAgo = now.AddMonths(-1);

                var stats = new ChatStatsDto
                {
                    WorkspaceId = workspaceId,
                    TotalMessages = await _context.ChatMessages
                        .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted),
                    
                    MessagesLast24Hours = await _context.ChatMessages
                        .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted && m.CreatedAt > oneDayAgo),
                    
                    MessagesLastWeek = await _context.ChatMessages
                        .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted && m.CreatedAt > oneWeekAgo),
                    
                    MessagesLastMonth = await _context.ChatMessages
                        .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted && m.CreatedAt > oneMonthAgo),
                    
                    ActiveUsers = await _context.ChatMessages
                        .Where(m => m.WorkspaceId == workspaceId && !m.IsDeleted && m.CreatedAt > oneWeekAgo)
                        .Select(m => m.UserId)
                        .Distinct()
                        .CountAsync(),
                    
                    TotalThreads = await _context.ChatMessages
                        .CountAsync(m => m.WorkspaceId == workspaceId && !m.IsDeleted && m.ParentMessageId != null),
                    
                    GeneratedAt = now
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating chat stats for workspace {WorkspaceId}", workspaceId);
                return new ChatStatsDto { WorkspaceId = workspaceId, GeneratedAt = DateTime.UtcNow };
            }
        }

        // Métodos auxiliares privados
        private async Task<ChatMessageDto> GetMessageDtoAsync(Guid messageId)
        {
            var message = await _context.ChatMessages
                .Where(m => m.Id == messageId)
                .Include(m => m.User)
                .Include(m => m.Reactions)
                    .ThenInclude(r => r.User)
                .Include(m => m.ReadReceipts)
                    .ThenInclude(r => r.User)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (message == null)
            {
                throw new InvalidOperationException($"Message {messageId} not found");
            }

            return ConvertToDto(message);
        }

        private ChatMessageDto ConvertToDto(ChatMessage message)
        {
            return new ChatMessageDto
            {
                Id = message.Id,
                Content = message.Content,
                Type = message.Type,
                WorkspaceId = message.WorkspaceId,
                ParentMessageId = message.ParentMessageId,
                AttachmentUrl = message.AttachmentUrl,
                AttachmentType = message.AttachmentType,
                AttachmentSize = message.AttachmentSize,
                IsEdited = message.IsEdited,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                User = new UserDto
                {
                    Id = message.User.Id,
                    Username = message.User.Username,
                    FirstName = message.User.FirstName,
                    LastName = message.User.LastName,
                    Avatar = message.User.Avatar,
                    Plan = message.User.Plan
                },
                Reactions = message.Reactions?.GroupBy(r => r.Emoji).Select(g => new MessageReactionSummaryDto
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    Users = g.Take(5).Select(r => new UserDto
                    {
                        Id = r.User.Id,
                        Username = r.User.Username,
                        Avatar = r.User.Avatar
                    }).ToList()
                }).ToList() ?? new List<MessageReactionSummaryDto>(),
                
                ReadBy = message.ReadReceipts?.Select(r => new UserDto
                {
                    Id = r.User.Id,
                    Username = r.User.Username,
                    Avatar = r.User.Avatar
                }).ToList() ?? new List<UserDto>(),
                
                ReplyCount = 0 // TODO: Implementar contagem de replies se necessário
            };
        }
    }
}
```

## 2. Service de Notificações

### 2.1 Interface do Notification Service

#### IDE.Application/Services/Notifications/INotificationService.cs
```csharp
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;

namespace IDE.Application.Services.Notifications
{
    /// <summary>
    /// Service para sistema de notificações
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Criar notificação personalizada
        /// </summary>
        Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request);
        
        /// <summary>
        /// Enviar notificação para workspace
        /// </summary>
        Task<NotificationDto> SendWorkspaceNotificationAsync(Guid workspaceId, NotificationType type, 
            string title, string message, Guid? senderId = null);
        
        /// <summary>
        /// Marcar notificação como lida
        /// </summary>
        Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
        
        /// <summary>
        /// Marcar todas as notificações como lidas para um usuário
        /// </summary>
        Task MarkAllAsReadAsync(Guid userId);
        
        /// <summary>
        /// Obter notificações do usuário
        /// </summary>
        Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int page = 1, int pageSize = 50);
        
        /// <summary>
        /// Obter contagem de notificações não lidas
        /// </summary>
        Task<int> GetUnreadCountAsync(Guid userId);
        
        /// <summary>
        /// Deletar notificação
        /// </summary>
        Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId);
        
        /// <summary>
        /// Limpar notificações antigas
        /// </summary>
        Task CleanupOldNotificationsAsync(TimeSpan maxAge);
        
        /// <summary>
        /// Obter estatísticas de notificações
        /// </summary>
        Task<NotificationStatsDto> GetNotificationStatsAsync(Guid? workspaceId = null);
        
        /// <summary>
        /// Enviar notificação de sistema
        /// </summary>
        Task<NotificationDto> SendSystemNotificationAsync(string title, string message, 
            NotificationPriority priority = NotificationPriority.Normal, List<Guid>? userIds = null);
    }
}
```

### 2.2 Implementação do Notification Service

#### IDE.Infrastructure/Services/Notifications/NotificationService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using IDE.Application.Services.Notifications;
using IDE.Domain.Entities.Realtime;
using IDE.Domain.Entities.Realtime.Enums;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;
using IDE.Infrastructure.Persistence.Data;
using System.Text.Json;

namespace IDE.Infrastructure.Services.Notifications
{
    /// <summary>
    /// Implementação do service de notificações
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext context,
            IDistributedCache cache,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request)
        {
            try
            {
                var notification = new RealtimeNotification
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = request.WorkspaceId,
                    UserId = request.UserId,
                    Type = request.Type,
                    Title = request.Title,
                    Content = request.Content,
                    Status = NotificationStatus.Unread,
                    Priority = request.Priority,
                    RelatedEntityId = request.RelatedEntityId,
                    RelatedEntityType = request.RelatedEntityType,
                    ActionUrl = request.ActionUrl,
                    ExpiresAt = request.ExpiresAt,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = request.Metadata
                };

                _context.RealtimeNotifications.Add(notification);
                await _context.SaveChangesAsync();

                // Invalidar cache do usuário
                await _cache.RemoveAsync($"user_notifications_{request.UserId}");

                _logger.LogInformation("Notification {NotificationId} created for user {UserId} in workspace {WorkspaceId}",
                    notification.Id, request.UserId, request.WorkspaceId);

                return MapToDto(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for user {UserId}", request.UserId);
                throw;
            }
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            try
            {
                var cacheKey = $"user_notifications_{userId}_{page}_{pageSize}";
                var cachedNotifications = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedNotifications))
                {
                    return JsonSerializer.Deserialize<List<NotificationDto>>(cachedNotifications) ?? new List<NotificationDto>();
                }

                var notifications = await _context.RealtimeNotifications
                    .Where(n => n.UserId == userId && 
                               (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow))
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtos = notifications.Select(MapToDto).ToList();

                // Cache por 2 minutos
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dtos), cacheOptions);

                return dtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
                throw;
            }
        }

        public async Task<NotificationDto?> GetNotificationAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var notification = await _context.RealtimeNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                return notification != null ? MapToDto(notification) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification {NotificationId} for user {UserId}", 
                    notificationId, userId);
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var notification = await _context.RealtimeNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    return false;
                }

                if (notification.Status == NotificationStatus.Read)
                {
                    return true; // Já estava marcada como lida
                }

                notification.Status = NotificationStatus.Read;
                notification.ReadAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                // Invalidar cache
                await _cache.RemoveAsync($"user_notifications_{userId}");

                _logger.LogInformation("Notification {NotificationId} marked as read by user {UserId}", 
                    notificationId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", 
                    notificationId, userId);
                throw;
            }
        }

        public async Task<int> MarkAllAsReadAsync(Guid userId, Guid? workspaceId = null)
        {
            try
            {
                var query = _context.RealtimeNotifications
                    .Where(n => n.UserId == userId && n.Status == NotificationStatus.Unread);

                if (workspaceId.HasValue)
                {
                    query = query.Where(n => n.WorkspaceId == workspaceId.Value);
                }

                var notifications = await query.ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.Status = NotificationStatus.Read;
                    notification.ReadAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Invalidar cache
                await _cache.RemoveAsync($"user_notifications_{userId}");

                _logger.LogInformation("Marked {Count} notifications as read for user {UserId} in workspace {WorkspaceId}",
                    notifications.Count, userId, workspaceId);

                return notifications.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var notification = await _context.RealtimeNotifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

                if (notification == null)
                {
                    return false;
                }

                _context.RealtimeNotifications.Remove(notification);
                await _context.SaveChangesAsync();

                // Invalidar cache
                await _cache.RemoveAsync($"user_notifications_{userId}");

                _logger.LogInformation("Notification {NotificationId} deleted by user {UserId}", 
                    notificationId, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId} for user {UserId}", 
                    notificationId, userId);
                throw;
            }
        }

        public async Task<int> GetUnreadCountAsync(Guid userId, Guid? workspaceId = null)
        {
            try
            {
                var cacheKey = $"unread_count_{userId}_{workspaceId}";
                var cachedCount = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedCount) && int.TryParse(cachedCount, out var count))
                {
                    return count;
                }

                var query = _context.RealtimeNotifications
                    .Where(n => n.UserId == userId && 
                               n.Status == NotificationStatus.Unread &&
                               (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow));

                if (workspaceId.HasValue)
                {
                    query = query.Where(n => n.WorkspaceId == workspaceId.Value);
                }

                var unreadCount = await query.CountAsync();

                // Cache por 1 minuto
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                };
                await _cache.SetStringAsync(cacheKey, unreadCount.ToString(), cacheOptions);

                return unreadCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<NotificationDto>> GetWorkspaceNotificationsAsync(Guid workspaceId, int page = 1, int pageSize = 20)
        {
            try
            {
                var notifications = await _context.RealtimeNotifications
                    .Where(n => n.WorkspaceId == workspaceId &&
                               (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow))
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return notifications.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for workspace {WorkspaceId}", workspaceId);
                throw;
            }
        }

        public async Task CleanupExpiredNotificationsAsync()
        {
            try
            {
                var expiredNotifications = await _context.RealtimeNotifications
                    .Where(n => n.ExpiresAt != null && n.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync();

                if (expiredNotifications.Any())
                {
                    _context.RealtimeNotifications.RemoveRange(expiredNotifications);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} expired notifications", expiredNotifications.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired notifications");
                throw;
            }
        }

        public async Task<NotificationStatsDto> GetNotificationStatsAsync(Guid userId, Guid? workspaceId = null)
        {
            try
            {
                var query = _context.RealtimeNotifications.Where(n => n.UserId == userId);

                if (workspaceId.HasValue)
                {
                    query = query.Where(n => n.WorkspaceId == workspaceId.Value);
                }

                var now = DateTime.UtcNow;
                var today = now.Date;

                var stats = await query
                    .GroupBy(n => 1)
                    .Select(g => new
                    {
                        TotalNotifications = g.Count(),
                        UnreadCount = g.Count(n => n.Status == NotificationStatus.Unread),
                        NotificationsToday = g.Count(n => n.CreatedAt >= today),
                        LastNotificationAt = g.Max(n => n.CreatedAt)
                    })
                    .FirstOrDefaultAsync();

                var typeDistribution = await query
                    .GroupBy(n => n.Type)
                    .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count());

                var priorityDistribution = await query
                    .Where(n => n.Status == NotificationStatus.Unread)
                    .GroupBy(n => n.Priority)
                    .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count());

                return new NotificationStatsDto
                {
                    TotalNotifications = stats?.TotalNotifications ?? 0,
                    UnreadCount = stats?.UnreadCount ?? 0,
                    ReadCount = (stats?.TotalNotifications ?? 0) - (stats?.UnreadCount ?? 0),
                    NotificationsToday = stats?.NotificationsToday ?? 0,
                    LastNotificationAt = stats?.LastNotificationAt,
                    TypeDistribution = typeDistribution,
                    PriorityDistribution = priorityDistribution,
                    AverageNotificationsPerDay = CalculateAverageNotificationsPerDay(userId, workspaceId)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification stats for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<NotificationDto>> GetNotificationsByTypeAsync(Guid userId, NotificationType type, int page = 1, int pageSize = 20)
        {
            try
            {
                var notifications = await _context.RealtimeNotifications
                    .Where(n => n.UserId == userId && n.Type == type &&
                               (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow))
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return notifications.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications by type {Type} for user {UserId}", type, userId);
                throw;
            }
        }

        #region Helper Methods

        private NotificationDto MapToDto(RealtimeNotification notification)
        {
            return new NotificationDto
            {
                Id = notification.Id,
                WorkspaceId = notification.WorkspaceId,
                UserId = notification.UserId,
                Type = notification.Type,
                Title = notification.Title,
                Content = notification.Content,
                Status = notification.Status,
                Priority = notification.Priority,
                RelatedEntityId = notification.RelatedEntityId,
                RelatedEntityType = notification.RelatedEntityType,
                ActionUrl = notification.ActionUrl,
                ExpiresAt = notification.ExpiresAt,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt,
                Metadata = notification.Metadata
            };
        }

        private double CalculateAverageNotificationsPerDay(Guid userId, Guid? workspaceId)
        {
            // Simplificado - retorna valor fixo
            // Em implementação real, calcularia com base no histórico
            return 5.0;
        }

        #endregion
    }
}
```
using IDE.Infrastructure.Data;

namespace IDE.Infrastructure.Services
{
    /// <summary>
    /// Implementação do service de notificações
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request)
        {
            try
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    Title = request.Title,
                    Message = request.Message,
                    Type = request.Type,
                    Priority = request.Priority,
                    WorkspaceId = request.WorkspaceId,
                    ActionUrl = request.ActionUrl,
                    ActionData = request.ActionData,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    ExpiresAt = CalculateExpirationDate(request.Priority)
                };

                _context.Notifications.Add(notification);

                // Criar registros para usuários específicos ou todos do workspace
                var targetUserIds = request.UserIds?.Any() == true 
                    ? request.UserIds 
                    : await GetWorkspaceUserIdsAsync(request.WorkspaceId);

                var userNotifications = targetUserIds.Select(userId => new UserNotification
                {
                    Id = Guid.NewGuid(),
                    NotificationId = notification.Id,
                    UserId = userId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.Set<UserNotification>().AddRange(userNotifications);
                await _context.SaveChangesAsync();

                var notificationDto = ConvertToDto(notification);

                _logger.LogInformation("Notification {NotificationId} created for {UserCount} users in workspace {WorkspaceId}",
                    notification.Id, targetUserIds.Count, request.WorkspaceId);

                return notificationDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for workspace {WorkspaceId}", request.WorkspaceId);
                throw;
            }
        }

        public async Task<NotificationDto> SendWorkspaceNotificationAsync(Guid workspaceId, NotificationType type, 
            string title, string message, Guid? senderId = null)
        {
            var request = new CreateNotificationRequest
            {
                Title = title,
                Message = message,
                Type = type,
                Priority = GetPriorityByType(type),
                WorkspaceId = workspaceId,
                UserIds = new List<Guid>() // Vazio = todos os usuários do workspace
            };

            return await CreateNotificationAsync(request);
        }

        public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var userNotification = await _context.Set<UserNotification>()
                    .FirstOrDefaultAsync(un => un.NotificationId == notificationId && un.UserId == userId);

                if (userNotification != null && !userNotification.IsRead)
                {
                    userNotification.IsRead = true;
                    userNotification.ReadAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Notification {NotificationId} marked as read by user {UserId}",
                        notificationId, userId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}",
                    notificationId, userId);
                return false;
            }
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            try
            {
                var unreadNotifications = await _context.Set<UserNotification>()
                    .Where(un => un.UserId == userId && !un.IsRead)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                foreach (var userNotification in unreadNotifications)
                {
                    userNotification.IsRead = true;
                    userNotification.ReadAt = now;
                }

                if (unreadNotifications.Any())
                {
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Marked {Count} notifications as read for user {UserId}",
                        unreadNotifications.Count, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int page = 1, int pageSize = 50)
        {
            try
            {
                var skip = (page - 1) * pageSize;
                
                var query = _context.Set<UserNotification>()
                    .Where(un => un.UserId == userId)
                    .Include(un => un.Notification)
                    .AsQueryable();

                if (unreadOnly)
                {
                    query = query.Where(un => !un.IsRead);
                }

                var userNotifications = await query
                    .OrderByDescending(un => un.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize)
                    .AsNoTracking()
                    .ToListAsync();

                return userNotifications.Select(un => ConvertToDto(un.Notification, un.IsRead, un.ReadAt)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
                return new List<NotificationDto>();
            }
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            try
            {
                return await _context.Set<UserNotification>()
                    .CountAsync(un => un.UserId == userId && !un.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
        {
            try
            {
                var userNotification = await _context.Set<UserNotification>()
                    .FirstOrDefaultAsync(un => un.NotificationId == notificationId && un.UserId == userId);

                if (userNotification != null)
                {
                    _context.Set<UserNotification>().Remove(userNotification);
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Notification {NotificationId} deleted for user {UserId}",
                        notificationId, userId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId} for user {UserId}",
                    notificationId, userId);
                return false;
            }
        }

        public async Task CleanupOldNotificationsAsync(TimeSpan maxAge)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow - maxAge;
                
                var oldNotifications = await _context.Notifications
                    .Where(n => n.CreatedAt < cutoffDate || (n.ExpiresAt.HasValue && n.ExpiresAt.Value < DateTime.UtcNow))
                    .ToListAsync();

                if (oldNotifications.Any())
                {
                    // Remover UserNotifications associadas
                    var notificationIds = oldNotifications.Select(n => n.Id).ToList();
                    var userNotifications = await _context.Set<UserNotification>()
                        .Where(un => notificationIds.Contains(un.NotificationId))
                        .ToListAsync();

                    _context.Set<UserNotification>().RemoveRange(userNotifications);
                    _context.Notifications.RemoveRange(oldNotifications);
                    
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {NotificationCount} old notifications and {UserNotificationCount} user notifications",
                        oldNotifications.Count, userNotifications.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old notifications");
            }
        }

        public async Task<NotificationStatsDto> GetNotificationStatsAsync(Guid? workspaceId = null)
        {
            try
            {
                var now = DateTime.UtcNow;
                var oneDayAgo = now.AddDays(-1);
                var oneWeekAgo = now.AddDays(-7);

                var query = _context.Notifications.AsQueryable();
                if (workspaceId.HasValue)
                {
                    query = query.Where(n => n.WorkspaceId == workspaceId.Value);
                }

                var stats = new NotificationStatsDto
                {
                    WorkspaceId = workspaceId,
                    TotalNotifications = await query.CountAsync(),
                    NotificationsLast24Hours = await query.CountAsync(n => n.CreatedAt > oneDayAgo),
                    NotificationsLastWeek = await query.CountAsync(n => n.CreatedAt > oneWeekAgo),
                    UnreadNotifications = await _context.Set<UserNotification>()
                        .CountAsync(un => !un.IsRead),
                    GeneratedAt = now
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating notification stats");
                return new NotificationStatsDto { GeneratedAt = DateTime.UtcNow };
            }
        }

        public async Task<NotificationDto> SendSystemNotificationAsync(string title, string message, 
            NotificationPriority priority = NotificationPriority.Normal, List<Guid>? userIds = null)
        {
            try
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Message = message,
                    Type = NotificationType.System,
                    Priority = priority,
                    WorkspaceId = null, // Notificação global
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    ExpiresAt = CalculateExpirationDate(priority)
                };

                _context.Notifications.Add(notification);

                // Se não especificou usuários, envia para todos os usuários ativos
                var targetUserIds = userIds ?? await GetAllActiveUserIdsAsync();

                var userNotifications = targetUserIds.Select(userId => new UserNotification
                {
                    Id = Guid.NewGuid(),
                    NotificationId = notification.Id,
                    UserId = userId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.Set<UserNotification>().AddRange(userNotifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation("System notification {NotificationId} sent to {UserCount} users",
                    notification.Id, targetUserIds.Count);

                return ConvertToDto(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system notification");
                throw;
            }
        }

        // Métodos auxiliares privados
        private async Task<List<Guid>> GetWorkspaceUserIdsAsync(Guid workspaceId)
        {
            return await _context.WorkspacePermissions
                .Where(wp => wp.WorkspaceId == workspaceId)
                .Select(wp => wp.UserId)
                .ToListAsync();
        }

        private async Task<List<Guid>> GetAllActiveUserIdsAsync()
        {
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            return await _context.Users
                .Where(u => u.LastLoginAt > oneWeekAgo) // Usuários ativos na última semana
                .Select(u => u.Id)
                .ToListAsync();
        }

        private NotificationPriority GetPriorityByType(NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => NotificationPriority.High,
                NotificationType.Warning => NotificationPriority.High,
                NotificationType.ChatMention => NotificationPriority.High,
                NotificationType.System => NotificationPriority.Normal,
                _ => NotificationPriority.Normal
            };
        }

        private DateTime? CalculateExpirationDate(NotificationPriority priority)
        {
            return priority switch
            {
                NotificationPriority.Low => DateTime.UtcNow.AddDays(3),
                NotificationPriority.Normal => DateTime.UtcNow.AddDays(7),
                NotificationPriority.High => DateTime.UtcNow.AddDays(14),
                _ => DateTime.UtcNow.AddDays(7)
            };
        }

        private NotificationDto ConvertToDto(Notification notification, bool isRead = false, DateTime? readAt = null)
        {
            return new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                Priority = notification.Priority,
                WorkspaceId = notification.WorkspaceId,
                ActionUrl = notification.ActionUrl,
                ActionData = notification.ActionData,
                IsRead = isRead,
                ReadAt = readAt,
                CreatedAt = notification.CreatedAt,
                ExpiresAt = notification.ExpiresAt
            };
        }
    }
}
```

## Entregáveis da Parte 3.8

✅ **IChatService**: Interface completa para operações de chat  
✅ **ChatService**: Implementação com mensagens, reações e threads  
✅ **INotificationService**: Interface para sistema de notificações  
✅ **NotificationService**: Implementação com cleanup automático  
✅ **Sistema de reações**: Emojis com contagem e usuários  
✅ **Sistema de leitura**: Marcação de mensagens como lidas  
✅ **Busca de mensagens**: Funcionalidade de search no chat  
✅ **Estatísticas**: Métricas de chat e notificações  

## Próximos Passos

Na **Parte 3.9**, implementaremos:
- IRateLimitingService para controle de taxa
- ICollaborationMetricsService para métricas
- ICollaborationAuditService para auditoria

**Dependência**: Esta parte (3.8) deve estar implementada antes de prosseguir.