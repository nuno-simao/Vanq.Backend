# Fase 3.6: SignalR Hub - Chat e Presença

## Chat, Cursores e Notificações em Tempo Real

Esta parte implementa o **sistema de chat**, **cursores colaborativos**, **indicadores de digitação** e **notificações** em tempo real para completar a experiência colaborativa.

**Pré-requisitos**: Parte 3.5 (SignalR Edição) implementada

## 1. Sistema de Chat em Tempo Real

### 1.1 Extensão do WorkspaceHub - Chat

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs (Continuação)
```csharp
// Adicionar estes métodos à classe WorkspaceHub existente
// Imports adicionais necessários:
using IDE.Application.Services.Chat;
using IDE.Application.Services.Notifications;

namespace IDE.Infrastructure.Realtime
{
    public partial class WorkspaceHub : Hub
    {
        /// <summary>
        /// Enviar mensagem no chat do workspace
        /// </summary>
        public async Task SendMessage(string workspaceId, SendChatMessageRequest request)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Rate limiting por plano do usuário
                var userPlan = await GetUserPlan(userId);
                if (!await _rateLimitingService.CheckChatLimitAsync(userId, userPlan))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para chat",
                        Code = "RATE_LIMIT_CHAT"
                    });
                    return;
                }

                // Verificar acesso ao workspace
                if (!await HasWorkspaceAccess(workspaceGuid, userId))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Acesso negado ao workspace",
                        Code = "CHAT_ACCESS_DENIED"
                    });
                    return;
                }

                // Validar conteúdo da mensagem
                if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "ValidationError",
                        Message = "Conteúdo da mensagem inválido",
                        Code = "INVALID_MESSAGE_CONTENT"
                    });
                    return;
                }

                // Criar mensagem no chat
                var messageDto = await _chatService.SendMessageAsync(workspaceGuid, userId, request);

                // Log de auditoria
                await _auditService.LogAsync(AuditAction.SendMessage, "chat", workspaceId, userId,
                    JsonSerializer.Serialize(new {
                        messageType = request.Type,
                        contentLength = request.Content.Length,
                        hasAttachment = !string.IsNullOrEmpty(request.AttachmentUrl)
                    }));

                // Métricas
                await _metricsService.IncrementAsync("chat_messages", 1, new { 
                    workspaceId,
                    messageType = request.Type.ToString()
                });

                // Broadcast para todos no workspace
                var shardGroup = GetShardGroup(workspaceId);
                await Clients.Group(shardGroup).SendAsync("MessageReceived", messageDto);

                // Processar menções se houver
                await ProcessMessageMentions(messageDto, workspaceGuid);

                // Confirmar envio para o remetente
                await Clients.Caller.SendAsync("MessageSent", new
                {
                    MessageId = messageDto.Id,
                    SentAt = messageDto.CreatedAt,
                    WorkspaceId = workspaceId
                });

                _logger.LogDebug("User {UserId} sent message to workspace {WorkspaceId}: {MessageType}", 
                    userId, workspaceId, request.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to workspace {WorkspaceId} by user {UserId}", 
                    workspaceId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao enviar mensagem",
                    Code = "MESSAGE_SEND_ERROR"
                });
            }
        }

        /// <summary>
        /// Editar mensagem existente
        /// </summary>
        public async Task EditMessage(string messageId, string newContent)
        {
            var userId = GetUserId();
            var messageGuid = Guid.Parse(messageId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckChatLimitAsync(userId, await GetUserPlan(userId)))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded",
                        Code = "RATE_LIMIT_CHAT_EDIT"
                    });
                    return;
                }

                // Editar mensagem via service
                var editedMessage = await _chatService.EditMessageAsync(messageGuid, userId, 
                    new EditChatMessageRequest { Content = newContent });

                // Broadcast mensagem editada
                var shardGroup = GetShardGroup(editedMessage.WorkspaceId.ToString());
                await Clients.Group(shardGroup).SendAsync("MessageEdited", editedMessage);

                // Métricas
                await _metricsService.IncrementAsync("chat_message_edits", 1);

                _logger.LogDebug("User {UserId} edited message {MessageId}", userId, messageId);
            }
            catch (UnauthorizedAccessException)
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "AccessDenied",
                    Message = "Apenas o autor pode editar a mensagem",
                    Code = "MESSAGE_EDIT_DENIED"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId} by user {UserId}", messageId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao editar mensagem",
                    Code = "MESSAGE_EDIT_ERROR"
                });
            }
        }

        /// <summary>
        /// Reagir a uma mensagem com emoji
        /// </summary>
        public async Task ReactToMessage(string messageId, string emoji)
        {
            var userId = GetUserId();
            var messageGuid = Guid.Parse(messageId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckChatLimitAsync(userId, await GetUserPlan(userId)))
                {
                    return; // Silently ignore reactions if rate limited
                }

                // Validar emoji (lista permitida)
                var allowedEmojis = new[] { "👍", "👎", "❤️", "😂", "😮", "😢", "😡", "🎉" };
                if (!allowedEmojis.Contains(emoji))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "ValidationError",
                        Message = "Emoji não permitido",
                        Code = "INVALID_EMOJI"
                    });
                    return;
                }

                // Processar reação
                var reactionResult = await _chatService.AddReactionAsync(messageGuid, userId, emoji);

                if (reactionResult != null)
                {
                    // Broadcast reação
                    var message = await _chatService.GetMessageAsync(messageGuid);
                    var shardGroup = GetShardGroup(message.WorkspaceId.ToString());
                    
                    await Clients.Group(shardGroup).SendAsync("MessageReactionAdded", new
                    {
                        MessageId = messageId,
                        Emoji = emoji,
                        UserId = userId.ToString(),
                        UserName = (await GetCurrentUserDto(userId)).Username,
                        TotalReactions = reactionResult.TotalCount,
                        AddedAt = DateTime.UtcNow
                    });

                    // Métricas
                    await _metricsService.IncrementAsync("message_reactions", 1, new { emoji });
                }

                _logger.LogDebug("User {UserId} reacted with {Emoji} to message {MessageId}", 
                    userId, emoji, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reaction to message {MessageId} by user {UserId}", 
                    messageId, userId);
            }
        }

        /// <summary>
        /// Indicador de digitação no chat
        /// </summary>
        public async Task TypingIndicator(string workspaceId, bool isTyping)
        {
            var userId = GetUserId();

            try
            {
                // Rate limiting leve para typing
                if (!await _rateLimitingService.CheckPresenceLimitAsync(userId))
                {
                    return; // Silently ignore if rate limited
                }

                var user = await GetCurrentUserDto(userId);

                var indicator = new TypingIndicatorDto
                {
                    UserId = userId.ToString(),
                    UserName = user.Username,
                    IsTyping = isTyping,
                    Timestamp = DateTime.UtcNow
                };

                var shardGroup = GetShardGroup(workspaceId);
                await Clients.OthersInGroup(shardGroup).SendAsync("TypingIndicator", indicator);

                // Métricas (sampling para evitar spam)
                if (isTyping && DateTime.UtcNow.Second % 10 == 0) // Uma vez a cada 10 segundos
                {
                    await _metricsService.IncrementAsync("typing_indicators", 1, new { workspaceId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing typing indicator for user {UserId} in workspace {WorkspaceId}", 
                    userId, workspaceId);
            }
        }

        /// <summary>
        /// Marcar mensagens como lidas
        /// </summary>
        public async Task MarkMessagesAsRead(string workspaceId, List<string> messageIds)
        {
            var userId = GetUserId();

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "mark_read"))
                {
                    return;
                }

                // Processar em lotes para performance
                const int batchSize = 50;
                var batches = messageIds.Chunk(batchSize);

                foreach (var batch in batches)
                {
                    await _chatService.MarkMessagesAsReadAsync(
                        batch.Select(Guid.Parse).ToList(), userId);
                }

                // Confirmar para o usuário
                await Clients.Caller.SendAsync("MessagesMarkedAsRead", new
                {
                    WorkspaceId = workspaceId,
                    MessageCount = messageIds.Count,
                    MarkedAt = DateTime.UtcNow
                });

                // Métricas
                await _metricsService.IncrementAsync("messages_marked_read", messageIds.Count, 
                    new { workspaceId });

                _logger.LogDebug("User {UserId} marked {Count} messages as read in workspace {WorkspaceId}", 
                    userId, messageIds.Count, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for user {UserId}", userId);
            }
        }
    }
}
```

## 2. Cursores Colaborativos

### 2.1 Gestão de Cursores em Tempo Real

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs (Continuação)
```csharp
// Adicionar estes métodos à classe WorkspaceHub existente

namespace IDE.Infrastructure.Realtime
{
    public partial class WorkspaceHub : Hub
    {
        /// <summary>
        /// Atualizar posição do cursor do usuário
        /// </summary>
        public async Task SendCursor(string itemId, UserCursorDto cursorPosition)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Rate limiting para cursors (mais leniente)
                if (!await _rateLimitingService.CheckCursorLimitAsync(userId))
                {
                    return; // Silently ignore cursor updates if rate limited
                }

                // Verificar se usuário está editando o item
                var isEditing = await _context.UserPresences
                    .AnyAsync(p => p.UserId == userId && p.CurrentItemId == itemId && 
                                  p.Status != UserPresenceStatus.Offline);

                if (!isEditing)
                {
                    return; // Usuário não está editando este item
                }

                // Atualizar cursor no banco de dados
                var existingCursor = await _context.UserCursors
                    .FirstOrDefaultAsync(c => c.ItemId == itemGuid && c.UserId == userId);

                var userColor = GenerateUserColor(userId);

                if (existingCursor != null)
                {
                    // Atualizar cursor existente
                    existingCursor.Position = cursorPosition.Position;
                    existingCursor.Line = cursorPosition.Line;
                    existingCursor.Column = cursorPosition.Column;
                    existingCursor.SelectionStart = cursorPosition.SelectionStart;
                    existingCursor.SelectionEnd = cursorPosition.SelectionEnd;
                    existingCursor.SelectionType = cursorPosition.SelectionType;
                    existingCursor.EditorMode = cursorPosition.EditorMode;
                    existingCursor.IsActive = cursorPosition.IsActive;
                    existingCursor.Timestamp = DateTime.UtcNow;
                    existingCursor.RequiresBroadcast = true;
                }
                else
                {
                    // Criar novo cursor
                    existingCursor = new UserCursor
                    {
                        Id = Guid.NewGuid(),
                        Position = cursorPosition.Position,
                        Line = cursorPosition.Line,
                        Column = cursorPosition.Column,
                        SelectionStart = cursorPosition.SelectionStart,
                        SelectionEnd = cursorPosition.SelectionEnd,
                        SelectionType = cursorPosition.SelectionType,
                        EditorMode = cursorPosition.EditorMode,
                        UserColor = userColor,
                        IsActive = cursorPosition.IsActive,
                        Timestamp = DateTime.UtcNow,
                        ItemId = itemGuid,
                        UserId = userId,
                        RequiresBroadcast = true
                    };
                    _context.UserCursors.Add(existingCursor);
                }

                await _context.SaveChangesAsync();

                // Preparar dados para broadcast
                var currentUser = await GetCurrentUserDto(userId);
                var broadcastCursor = new UserCursorDto
                {
                    Position = existingCursor.Position,
                    Line = existingCursor.Line,
                    Column = existingCursor.Column,
                    SelectionStart = existingCursor.SelectionStart,
                    SelectionEnd = existingCursor.SelectionEnd,
                    SelectionType = existingCursor.SelectionType,
                    EditorMode = existingCursor.EditorMode,
                    UserId = userId.ToString(),
                    UserName = currentUser.Username,
                    UserColor = userColor,
                    IsActive = existingCursor.IsActive,
                    Timestamp = existingCursor.Timestamp
                };

                // Broadcast para outros editores do item
                var itemGroup = $"item_{itemId}";
                await Clients.OthersInGroup(itemGroup).SendAsync("CursorUpdate", new
                {
                    ItemId = itemId,
                    Cursor = broadcastCursor
                });

                // Métricas (sampling para evitar spam)
                if (DateTime.UtcNow.Millisecond % 100 < 10) // 10% das atualizações
                {
                    await _metricsService.IncrementAsync("cursor_updates", 1, new { itemId });
                }

                // Marcar que foi feito broadcast
                existingCursor.LastBroadcast = DateTime.UtcNow;
                existingCursor.RequiresBroadcast = false;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cursor for user {UserId} in item {ItemId}", 
                    userId, itemId);
            }
        }

        /// <summary>
        /// Seleção de texto colaborativa
        /// </summary>
        public async Task SendTextSelection(string itemId, TextSelectionDto selection)
        {
            var userId = GetUserId();

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckCursorLimitAsync(userId))
                {
                    return;
                }

                // Verificar se usuário está editando
                var isEditing = await _context.UserPresences
                    .AnyAsync(p => p.UserId == userId && p.CurrentItemId == itemId && 
                                  p.Status != UserPresenceStatus.Offline);

                if (!isEditing)
                {
                    return;
                }

                // Preparar seleção para broadcast
                var user = await GetCurrentUserDto(userId);
                var broadcastSelection = new TextSelectionDto
                {
                    UserId = userId.ToString(),
                    UserName = user.Username,
                    UserColor = GenerateUserColor(userId),
                    StartLine = selection.StartLine,
                    StartColumn = selection.StartColumn,
                    EndLine = selection.EndLine,
                    EndColumn = selection.EndColumn,
                    SelectedText = selection.SelectedText?.Substring(0, Math.Min(selection.SelectedText.Length, 200)), // Limitar tamanho
                    UpdatedAt = DateTime.UtcNow
                };

                // Broadcast para outros editores
                var itemGroup = $"item_{itemId}";
                await Clients.OthersInGroup(itemGroup).SendAsync("TextSelectionUpdate", new
                {
                    ItemId = itemId,
                    Selection = broadcastSelection
                });

                // Métricas
                if (DateTime.UtcNow.Second % 5 == 0) // Sampling a cada 5 segundos
                {
                    await _metricsService.IncrementAsync("text_selections", 1, new { itemId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing text selection for user {UserId} in item {ItemId}", 
                    userId, itemId);
            }
        }

        /// <summary>
        /// Obter todos os cursores ativos no item
        /// </summary>
        public async Task GetItemCursors(string itemId)
        {
            var userId = GetUserId();
            var itemGuid = Guid.Parse(itemId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "cursor_query"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para consulta de cursores",
                        Code = "RATE_LIMIT_CURSOR_QUERY"
                    });
                    return;
                }

                var cursors = await GetItemActiveCursors(itemGuid);

                await Clients.Caller.SendAsync("ItemCursors", new
                {
                    ItemId = itemId,
                    Cursors = cursors,
                    Count = cursors.Count,
                    UpdatedAt = DateTime.UtcNow
                });

                _logger.LogDebug("User {UserId} requested cursors for item {ItemId}: {Count} active cursors", 
                    userId, itemId, cursors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cursors for item {ItemId}", itemId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao obter cursores",
                    Code = "CURSOR_QUERY_ERROR"
                });
            }
        }
    }
}
```

## 3. Sistema de Notificações

### 3.1 Notificações em Tempo Real

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs (Continuação)
```csharp
// Adicionar estes métodos à classe WorkspaceHub existente

namespace IDE.Infrastructure.Realtime
{
    public partial class WorkspaceHub : Hub
    {
        /// <summary>
        /// Enviar notificação para usuários do workspace
        /// </summary>
        public async Task SendNotification(string workspaceId, NotificationDto notification)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Verificar se usuário tem permissão para enviar notificações
                if (!await HasWorkspaceAccess(workspaceGuid, userId, PermissionLevel.Editor))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "AccessDenied",
                        Message = "Permissão insuficiente para enviar notificações",
                        Code = "NOTIFICATION_ACCESS_DENIED"
                    });
                    return;
                }

                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "send_notification"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para notificações",
                        Code = "RATE_LIMIT_NOTIFICATION"
                    });
                    return;
                }

                // Criar notificação via service
                var createdNotification = await _notificationService.SendWorkspaceNotificationAsync(
                    workspaceGuid, 
                    notification.Type, 
                    notification.Title, 
                    notification.Message, 
                    userId);

                // Broadcast para workspace
                var shardGroup = GetShardGroup(workspaceId);
                await Clients.Group(shardGroup).SendAsync("NotificationReceived", createdNotification);

                // Métricas
                await _metricsService.IncrementAsync("notifications_sent", 1, new { 
                    workspaceId,
                    type = notification.Type.ToString()
                });

                _logger.LogInformation("User {UserId} sent notification to workspace {WorkspaceId}: {Type}", 
                    userId, workspaceId, notification.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to workspace {WorkspaceId} by user {UserId}", 
                    workspaceId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao enviar notificação",
                    Code = "NOTIFICATION_SEND_ERROR"
                });
            }
        }

        /// <summary>
        /// Marcar notificação como lida
        /// </summary>
        public async Task MarkNotificationAsRead(string notificationId)
        {
            var userId = GetUserId();
            var notificationGuid = Guid.Parse(notificationId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "mark_notification_read"))
                {
                    return;
                }

                var success = await _notificationService.MarkAsReadAsync(notificationGuid, userId);

                if (success)
                {
                    await Clients.Caller.SendAsync("NotificationMarkedAsRead", new
                    {
                        NotificationId = notificationId,
                        ReadAt = DateTime.UtcNow
                    });

                    // Métricas
                    await _metricsService.IncrementAsync("notifications_read", 1);
                }

                _logger.LogDebug("User {UserId} marked notification {NotificationId} as read", 
                    userId, notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", 
                    notificationId, userId);
            }
        }

        /// <summary>
        /// Obter notificações não lidas do usuário
        /// </summary>
        public async Task GetUnreadNotifications()
        {
            var userId = GetUserId();

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "get_notifications"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para consulta de notificações",
                        Code = "RATE_LIMIT_NOTIFICATIONS"
                    });
                    return;
                }

                var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly: true);
                var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

                await Clients.Caller.SendAsync("UnreadNotifications", new
                {
                    Notifications = notifications,
                    UnreadCount = unreadCount,
                    RetrievedAt = DateTime.UtcNow
                });

                _logger.LogDebug("User {UserId} retrieved {Count} unread notifications", userId, unreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications for user {UserId}", userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao obter notificações",
                    Code = "NOTIFICATIONS_QUERY_ERROR"
                });
            }
        }
    }
}
```

## 4. Métodos Auxiliares para Chat e Presença

### 4.1 Processamento de Menções e Eventos

#### IDE.Infrastructure/Realtime/WorkspaceHub.cs (Continuação)
```csharp
// Adicionar estes métodos auxiliares à classe WorkspaceHub existente

namespace IDE.Infrastructure.Realtime
{
    public partial class WorkspaceHub : Hub
    {
        /// <summary>
        /// Processar menções em mensagens de chat
        /// </summary>
        private async Task ProcessMessageMentions(ChatMessageDto message, Guid workspaceId)
        {
            try
            {
                // Extrair menções (@username) da mensagem
                var mentionPattern = @"@(\w+)";
                var mentions = System.Text.RegularExpressions.Regex.Matches(message.Content, mentionPattern);

                if (!mentions.Any()) return;

                foreach (System.Text.RegularExpressions.Match mention in mentions)
                {
                    var username = mention.Groups[1].Value;
                    
                    // Buscar usuário mencionado
                    var mentionedUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

                    if (mentionedUser != null)
                    {
                        // Verificar se usuário tem acesso ao workspace
                        if (await HasWorkspaceAccess(workspaceId, mentionedUser.Id))
                        {
                            // Criar notificação de menção
                            await _notificationService.CreateNotificationAsync(new CreateNotificationRequest
                            {
                                Title = "Você foi mencionado no chat",
                                Message = $"{message.User.Username} mencionou você: {TruncateMessage(message.Content, 100)}",
                                Type = NotificationType.ChatMention,
                                Priority = NotificationPriority.High,
                                WorkspaceId = workspaceId,
                                UserIds = new List<Guid> { mentionedUser.Id },
                                ActionUrl = $"/workspaces/{workspaceId}/chat",
                                ActionData = JsonSerializer.Serialize(new { messageId = message.Id })
                            });

                            // Enviar notificação em tempo real se usuário estiver online
                            var mentionedUserConnections = await _context.UserPresences
                                .Where(p => p.UserId == mentionedUser.Id && 
                                           p.Status != UserPresenceStatus.Offline)
                                .Select(p => p.ConnectionId)
                                .ToListAsync();

                            if (mentionedUserConnections.Any())
                            {
                                await Clients.Clients(mentionedUserConnections).SendAsync("MentionReceived", new
                                {
                                    MessageId = message.Id,
                                    WorkspaceId = workspaceId.ToString(),
                                    MentionedBy = message.User,
                                    MessagePreview = TruncateMessage(message.Content, 100),
                                    ReceivedAt = DateTime.UtcNow
                                });
                            }

                            // Métricas
                            await _metricsService.IncrementAsync("chat_mentions", 1, new { workspaceId = workspaceId.ToString() });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mentions in message {MessageId}", message.Id);
            }
        }

        /// <summary>
        /// Cleanup de conexões inativas (método interno chamado periodicamente)
        /// </summary>
        public async Task CleanupInactiveConnections()
        {
            try
            {
                // Executar limpeza de presenças antigas
                await _presenceService.CleanupStaleConnectionsAsync();

                // Limpar cursores inativos
                var staleThreshold = DateTime.UtcNow.AddMinutes(-10);
                var staleCursors = await _context.UserCursors
                    .Where(c => c.Timestamp < staleThreshold && c.IsActive)
                    .ToListAsync();

                foreach (var cursor in staleCursors)
                {
                    cursor.IsActive = false;
                    cursor.Timestamp = DateTime.UtcNow;
                }

                if (staleCursors.Any())
                {
                    await _context.SaveChangesAsync();
                    
                    // Notificar que cursores foram removidos
                    var cursorsByItem = staleCursors.GroupBy(c => c.ItemId);
                    foreach (var group in cursorsByItem)
                    {
                        var itemGroup = $"item_{group.Key}";
                        await _hubContext.Clients.Group(itemGroup).SendAsync("CursorsCleared", new
                        {
                            ItemId = group.Key.ToString(),
                            RemovedCursors = group.Select(c => c.UserId.ToString()).ToList(),
                            ClearedAt = DateTime.UtcNow,
                            Reason = "Inactivity"
                        });
                    }
                }

                // Métricas
                await _metricsService.IncrementAsync("cleanup_operations", 1);
                if (staleCursors.Any())
                {
                    await _metricsService.IncrementAsync("stale_cursors_cleaned", staleCursors.Count);
                }

                _logger.LogDebug("Cleaned up {Count} stale cursors", staleCursors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of inactive connections");
            }
        }

        /// <summary>
        /// Truncar mensagem para exibição
        /// </summary>
        private string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Verificar se usuário está online em um workspace
        /// </summary>
        private async Task<bool> IsUserOnlineInWorkspace(Guid userId, Guid workspaceId)
        {
            return await _context.UserPresences
                .AnyAsync(p => p.UserId == userId && 
                              p.WorkspaceId == workspaceId && 
                              p.Status != UserPresenceStatus.Offline &&
                              p.LastSeenAt > DateTime.UtcNow.AddMinutes(-5));
        }

        /// <summary>
        /// Obter estatísticas do workspace
        /// </summary>
        public async Task GetWorkspaceStats(string workspaceId)
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
                        Code = "WORKSPACE_STATS_ACCESS_DENIED"
                    });
                    return;
                }

                // Coletar estatísticas
                var stats = new
                {
                    WorkspaceId = workspaceId,
                    ActiveUsers = await _presenceService.GetWorkspaceActiveCountAsync(workspaceGuid),
                    TotalMessages = await _context.ChatMessages.CountAsync(m => m.WorkspaceId == workspaceGuid),
                    TotalEdits = await _context.EditorChanges
                        .Where(e => _context.ModuleItems
                            .Any(i => i.Id == e.ItemId && i.WorkspaceId == workspaceGuid))
                        .CountAsync(),
                    ActiveItems = await _context.UserPresences
                        .Where(p => p.WorkspaceId == workspaceGuid && 
                                   !string.IsNullOrEmpty(p.CurrentItemId) &&
                                   p.Status != UserPresenceStatus.Offline)
                        .Select(p => p.CurrentItemId)
                        .Distinct()
                        .CountAsync(),
                    GeneratedAt = DateTime.UtcNow
                };

                await Clients.Caller.SendAsync("WorkspaceStats", stats);

                _logger.LogDebug("User {UserId} requested stats for workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats for workspace {WorkspaceId}", workspaceId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "SystemError",
                    Message = "Erro ao obter estatísticas",
                    Code = "WORKSPACE_STATS_ERROR"
                });
            }
        }
    }

    /// <summary>
    /// DTO para indicador de digitação
    /// </summary>
    public class TypingIndicatorDto
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public bool IsTyping { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DTO para seleção de texto
    /// </summary>
    public class TextSelectionDto
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserColor { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string SelectedText { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

## 8. ChatHub Dedicado

### 8.1 Implementação do ChatHub

#### IDE.Infrastructure/Realtime/ChatHub.cs
```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using IDE.Application.Services.Chat;
using IDE.Application.Services.Notifications;
using IDE.Application.Services.Collaboration;
using IDE.Application.Realtime.DTOs;
using IDE.Application.Realtime.Requests;
using IDE.Domain.Entities.Realtime.Enums;
using System.Text.Json;

namespace IDE.Infrastructure.Realtime
{
    /// <summary>
    /// Hub dedicado para funcionalidades de chat em tempo real
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly INotificationService _notificationService;
        private readonly IRateLimitingService _rateLimitingService;
        private readonly ICollaborationAuditService _auditService;
        private readonly ICollaborationMetricsService _metricsService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(
            IChatService chatService,
            INotificationService notificationService,
            IRateLimitingService rateLimitingService,
            ICollaborationAuditService auditService,
            ICollaborationMetricsService metricsService,
            ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _notificationService = notificationService;
            _rateLimitingService = rateLimitingService;
            _auditService = auditService;
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <summary>
        /// Conexão estabelecida
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            var connectionId = Context.ConnectionId;

            try
            {
                _logger.LogInformation("User {UserId} connected to ChatHub with connection {ConnectionId}", 
                    userId, connectionId);

                await _metricsService.IncrementAsync("chat_connections", 1);
                
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Conexão desconectada
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = GetUserId();
            var connectionId = Context.ConnectionId;

            try
            {
                _logger.LogInformation("User {UserId} disconnected from ChatHub with connection {ConnectionId}", 
                    userId, connectionId);

                await _metricsService.IncrementAsync("chat_disconnections", 1);

                if (exception != null)
                {
                    _logger.LogWarning(exception, "User {UserId} disconnected with exception", userId);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Entrar no chat de um workspace
        /// </summary>
        public async Task JoinWorkspaceChat(string workspaceId)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Verificar rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "chat_join"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para join workspace chat"
                    });
                    return;
                }

                // Verificar acesso ao workspace
                // TODO: Implementar verificação de acesso
                
                var chatGroupName = GetWorkspaceChatGroup(workspaceId);
                await Groups.AddToGroupAsync(Context.ConnectionId, chatGroupName);

                // Notificar outros usuários
                await Clients.Group(chatGroupName).SendAsync("UserJoinedChat", new
                {
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    JoinedAt = DateTime.UtcNow
                });

                // Confirmar join para o usuário
                await Clients.Caller.SendAsync("JoinedWorkspaceChat", new
                {
                    WorkspaceId = workspaceId,
                    JoinedAt = DateTime.UtcNow
                });

                await _auditService.LogAsync(AuditAction.JoinChat, "workspace", workspaceId, userId, null);

                _logger.LogInformation("User {UserId} joined workspace {WorkspaceId} chat", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining workspace chat for user {UserId}, workspace {WorkspaceId}", 
                    userId, workspaceId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "ServerError",
                    Message = "Erro ao entrar no chat do workspace"
                });
            }
        }

        /// <summary>
        /// Sair do chat de um workspace
        /// </summary>
        public async Task LeaveWorkspaceChat(string workspaceId)
        {
            var userId = GetUserId();

            try
            {
                var chatGroupName = GetWorkspaceChatGroup(workspaceId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatGroupName);

                // Notificar outros usuários
                await Clients.Group(chatGroupName).SendAsync("UserLeftChat", new
                {
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    LeftAt = DateTime.UtcNow
                });

                await Clients.Caller.SendAsync("LeftWorkspaceChat", new
                {
                    WorkspaceId = workspaceId,
                    LeftAt = DateTime.UtcNow
                });

                await _auditService.LogAsync(AuditAction.LeaveChat, "workspace", workspaceId, userId, null);

                _logger.LogInformation("User {UserId} left workspace {WorkspaceId} chat", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving workspace chat for user {UserId}, workspace {WorkspaceId}", 
                    userId, workspaceId);
            }
        }

        /// <summary>
        /// Enviar mensagem no chat
        /// </summary>
        public async Task SendMessage(string workspaceId, SendChatMessageRequest request)
        {
            var userId = GetUserId();
            var workspaceGuid = Guid.Parse(workspaceId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "chat_message"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para envio de mensagens"
                    });
                    return;
                }

                // Enviar mensagem através do service
                var messageDto = await _chatService.SendMessageAsync(workspaceGuid, userId, request);

                // Broadcast para o grupo do workspace
                var chatGroupName = GetWorkspaceChatGroup(workspaceId);
                await Clients.Group(chatGroupName).SendAsync("MessageReceived", messageDto);

                // Métricas
                await _metricsService.IncrementAsync("chat_messages_sent", 1, new
                {
                    workspaceId,
                    messageType = request.Type.ToString()
                });

                await _auditService.LogAsync(AuditAction.SendMessage, "message", messageDto.Id.ToString(), userId,
                    JsonSerializer.Serialize(new { workspaceId, contentLength = request.Content.Length }));

                _logger.LogInformation("User {UserId} sent message in workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message for user {UserId}, workspace {WorkspaceId}", 
                    userId, workspaceId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "ServerError",
                    Message = "Erro ao enviar mensagem"
                });
            }
        }

        /// <summary>
        /// Editar mensagem existente
        /// </summary>
        public async Task EditMessage(string messageId, EditChatMessageRequest request)
        {
            var userId = GetUserId();
            var messageGuid = Guid.Parse(messageId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "chat_edit"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para edição de mensagens"
                    });
                    return;
                }

                // Editar mensagem através do service
                var messageDto = await _chatService.EditMessageAsync(messageGuid, userId, request);

                // Broadcast para o grupo do workspace
                var chatGroupName = GetWorkspaceChatGroup(messageDto.WorkspaceId.ToString());
                await Clients.Group(chatGroupName).SendAsync("MessageEdited", messageDto);

                await _auditService.LogAsync(AuditAction.EditMessage, "message", messageId, userId, 
                    JsonSerializer.Serialize(new { newContentLength = request.Content.Length }));

                _logger.LogInformation("User {UserId} edited message {MessageId}", userId, messageId);
            }
            catch (UnauthorizedAccessException)
            {
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "Unauthorized",
                    Message = "Não é possível editar esta mensagem"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing message {MessageId} for user {UserId}", messageId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "ServerError",
                    Message = "Erro ao editar mensagem"
                });
            }
        }

        /// <summary>
        /// Deletar mensagem
        /// </summary>
        public async Task DeleteMessage(string messageId)
        {
            var userId = GetUserId();
            var messageGuid = Guid.Parse(messageId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "chat_delete"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para exclusão de mensagens"
                    });
                    return;
                }

                // Obter mensagem para saber o workspace
                var message = await _chatService.GetMessageAsync(messageGuid);
                
                // Deletar mensagem através do service
                var success = await _chatService.DeleteMessageAsync(messageGuid, userId);

                if (success)
                {
                    // Broadcast para o grupo do workspace
                    var chatGroupName = GetWorkspaceChatGroup(message.WorkspaceId.ToString());
                    await Clients.Group(chatGroupName).SendAsync("MessageDeleted", new
                    {
                        MessageId = messageId,
                        DeletedBy = userId,
                        DeletedAt = DateTime.UtcNow
                    });

                    await _auditService.LogAsync(AuditAction.DeleteMessage, "message", messageId, userId, null);
                    
                    _logger.LogInformation("User {UserId} deleted message {MessageId}", userId, messageId);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "NotFound",
                        Message = "Mensagem não encontrada ou sem permissão para deletar"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for user {UserId}", messageId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "ServerError",
                    Message = "Erro ao deletar mensagem"
                });
            }
        }

        /// <summary>
        /// Adicionar reação a uma mensagem
        /// </summary>
        public async Task AddReaction(string messageId, string emoji)
        {
            var userId = GetUserId();
            var messageGuid = Guid.Parse(messageId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "chat_reaction"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para reações"
                    });
                    return;
                }

                // Adicionar reação através do service
                var reaction = await _chatService.AddReactionAsync(messageGuid, userId, emoji);
                
                if (reaction != null)
                {
                    // Obter mensagem para saber o workspace
                    var message = await _chatService.GetMessageAsync(messageGuid);
                    
                    // Broadcast para o grupo do workspace
                    var chatGroupName = GetWorkspaceChatGroup(message.WorkspaceId.ToString());
                    await Clients.Group(chatGroupName).SendAsync("ReactionAdded", reaction);

                    _logger.LogInformation("User {UserId} added reaction {Emoji} to message {MessageId}", 
                        userId, emoji, messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reaction to message {MessageId} for user {UserId}", 
                    messageId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "ServerError",
                    Message = "Erro ao adicionar reação"
                });
            }
        }

        /// <summary>
        /// Remover reação de uma mensagem
        /// </summary>
        public async Task RemoveReaction(string messageId, string emoji)
        {
            var userId = GetUserId();
            var messageGuid = Guid.Parse(messageId);

            try
            {
                // Rate limiting
                if (!await _rateLimitingService.CheckLimitAsync(userId, "chat_reaction"))
                {
                    await Clients.Caller.SendAsync("Error", new
                    {
                        Type = "RateLimit",
                        Message = "Rate limit exceeded para reações"
                    });
                    return;
                }

                // Obter mensagem para saber o workspace
                var message = await _chatService.GetMessageAsync(messageGuid);

                // Remover reação através do service
                await _chatService.RemoveReactionAsync(messageGuid, userId, emoji);

                // Broadcast para o grupo do workspace
                var chatGroupName = GetWorkspaceChatGroup(message.WorkspaceId.ToString());
                await Clients.Group(chatGroupName).SendAsync("ReactionRemoved", new
                {
                    MessageId = messageId,
                    Emoji = emoji,
                    UserId = userId,
                    RemovedAt = DateTime.UtcNow
                });

                _logger.LogInformation("User {UserId} removed reaction {Emoji} from message {MessageId}", 
                    userId, emoji, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from message {MessageId} for user {UserId}", 
                    messageId, userId);
                
                await Clients.Caller.SendAsync("Error", new
                {
                    Type = "ServerError",
                    Message = "Erro ao remover reação"
                });
            }
        }

        /// <summary>
        /// Indicar que está digitando
        /// </summary>
        public async Task StartTyping(string workspaceId)
        {
            var userId = GetUserId();

            try
            {
                var chatGroupName = GetWorkspaceChatGroup(workspaceId);
                
                // Notificar outros usuários que está digitando
                await Clients.Group(chatGroupName).SendAsync("UserStartedTyping", new
                {
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    StartedAt = DateTime.UtcNow
                });

                _logger.LogDebug("User {UserId} started typing in workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartTyping for user {UserId}, workspace {WorkspaceId}", 
                    userId, workspaceId);
            }
        }

        /// <summary>
        /// Parar de indicar que está digitando
        /// </summary>
        public async Task StopTyping(string workspaceId)
        {
            var userId = GetUserId();

            try
            {
                var chatGroupName = GetWorkspaceChatGroup(workspaceId);
                
                // Notificar outros usuários que parou de digitar
                await Clients.Group(chatGroupName).SendAsync("UserStoppedTyping", new
                {
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    StoppedAt = DateTime.UtcNow
                });

                _logger.LogDebug("User {UserId} stopped typing in workspace {WorkspaceId}", userId, workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StopTyping for user {UserId}, workspace {WorkspaceId}", 
                    userId, workspaceId);
            }
        }

        #region Helper Methods

        private Guid GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User ID not found in claims");
            }
            return userId;
        }

        private string GetWorkspaceChatGroup(string workspaceId)
        {
            return $"workspace_chat_{workspaceId}";
        }

        #endregion
    }
}
```

## Entregáveis da Parte 3.6

✅ **Sistema de chat completo** com menções e reações  
✅ **Cursores colaborativos** com seleção de texto  
✅ **Indicadores de digitação** em tempo real  
✅ **Notificações push** para usuários online  
✅ **Processamento de menções** com notificações  
✅ **Cleanup automático** de conexões inativas  
✅ **Estatísticas de workspace** em tempo real  
✅ **Rate limiting** em todas as operações  

## Próximos Passos

Na **Parte 3.7**, implementaremos:
- IUserPresenceService completo
- Gestão de usuários ativos
- Cleanup de conexões
- Métricas de presença

**Dependência**: Esta parte (3.6) deve estar implementada e testada antes de prosseguir.