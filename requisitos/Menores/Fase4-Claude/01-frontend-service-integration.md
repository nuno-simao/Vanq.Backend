# Fase 4 - Parte 1: Frontend Service Integration

## Contexto da Implementação

Esta é a **primeira parte da Fase 4** focada na **integração completa** do frontend React com o backend .NET Core. Substituiremos completamente os dados mock por APIs reais.

### Objetivos da Parte 1
✅ **Substituir WorkspaceService mock** por chamadas HTTP reais  
✅ **Implementar SignalR client** para tempo real  
✅ **Integrar autenticação JWT** completa  
✅ **Adaptar hooks React** para consumir APIs REST  
✅ **Manter compatibilidade** com contextos existentes  

### Pré-requisitos
- Backend .NET Core 8 das Fases 1-3 funcionando
- Frontend React com dados mock funcionais
- Endpoints de API documentados e testados

---

## 0. Base Interfaces & Dependency Injection Setup

### 0.1 Interfaces Base Necessárias

Antes de implementar os services, precisamos definir todas as interfaces que serão usadas nos próximos arquivos da Fase 4.

#### IDE.Application/Common/Interfaces/IRedisCacheService.cs
```csharp
using IDE.Application.Common.Interfaces;

namespace IDE.Infrastructure.Caching
{
    public interface IRedisCacheService : ICacheService
    {
        Task<List<string>> GetKeysAsync(string pattern);
        Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiry = null);
        Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<T> GetAndDeleteAsync<T>(string key);
        Task<List<T>> GetMultipleAsync<T>(IEnumerable<string> keys);
        Task SetMultipleAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiry = null);
        Task<long> GetTtlAsync(string key);
        Task<bool> RefreshTtlAsync(string key, TimeSpan expiry);
    }
}
```

#### IDE.Application/Common/Interfaces/ICacheInvalidationService.cs
```csharp
namespace IDE.Application.Common.Interfaces
{
    public interface ICacheInvalidationService
    {
        Task InvalidateWorkspaceAsync(Guid workspaceId);
        Task InvalidateUserAsync(Guid userId);
        Task InvalidatePatternAsync(string pattern);
        Task InvalidateMultipleAsync(IEnumerable<string> keys);
        Task InvalidateWorkspaceItemsAsync(Guid workspaceId);
        Task InvalidateChatAsync(Guid workspaceId);
        Task InvalidatePresenceAsync(Guid workspaceId);
        Task InvalidateCollaborationSessionAsync(Guid sessionId);
    }
}
```

#### IDE.Application/Realtime/Services/ICollaborationService.cs
```csharp
using IDE.Application.Realtime.Models;

namespace IDE.Application.Realtime.Services
{
    public interface ICollaborationService
    {
        Task<CollaborationSession> StartSessionAsync(Guid workspaceId, Guid userId);
        Task EndSessionAsync(Guid sessionId);
        Task<ItemChangeRecord> ProcessItemChangeAsync(Guid workspaceId, Guid itemId, ItemChange change);
        Task<List<ItemChangeRecord>> GetItemHistoryAsync(Guid itemId, int limit = 50);
        Task<ConflictResolution> ResolveConflictAsync(Guid itemId, List<ItemChange> conflictingChanges);
        Task NotifyItemChangedAsync(Guid workspaceId, Guid itemId, string changeType);
        Task<List<CollaborationSession>> GetActiveSessionsAsync(Guid workspaceId);
        Task<bool> IsUserActiveInWorkspaceAsync(Guid workspaceId, Guid userId);
    }
}
```

#### IDE.Application/Realtime/Services/IUserPresenceService.cs
```csharp
using IDE.Application.Realtime.Models;

namespace IDE.Application.Realtime.Services
{
    public interface IUserPresenceService
    {
        Task<UserPresence> UpdatePresenceAsync(Guid workspaceId, Guid userId, string connectionId, string status = "online");
        Task<List<UserPresence>> GetWorkspacePresenceAsync(Guid workspaceId);
        Task RemovePresenceAsync(string connectionId);
        Task UpdateTypingStatusAsync(Guid workspaceId, Guid userId, bool isTyping, string itemId = null);
        Task<List<TypingIndicator>> GetTypingUsersAsync(Guid workspaceId, string itemId = null);
        Task CleanupStaleConnectionsAsync();
        Task<bool> IsUserOnlineAsync(Guid userId, Guid workspaceId);
        Task<int> GetActiveUsersCountAsync(Guid workspaceId);
    }
}
```

#### IDE.Application/Realtime/Services/IChatService.cs
```csharp
using IDE.Application.Realtime.Models;

namespace IDE.Application.Realtime.Services
{
    public interface IChatService
    {
        Task<ChatMessage> SendMessageAsync(Guid workspaceId, Guid userId, string content, ChatMessageType type = ChatMessageType.Text);
        Task<List<ChatMessage>> GetRecentMessagesAsync(Guid workspaceId, int count = 50);
        Task<List<ChatMessage>> GetMessageHistoryAsync(Guid workspaceId, DateTime? before = null, int count = 50);
        Task<ChatMessage> UpdateMessageAsync(Guid messageId, string newContent);
        Task DeleteMessageAsync(Guid messageId, Guid userId);
        Task<ChatReaction> AddReactionAsync(Guid messageId, Guid userId, string emoji);
        Task RemoveReactionAsync(Guid messageId, Guid userId, string emoji);
        Task<List<ChatMessage>> SearchMessagesAsync(Guid workspaceId, string query, int count = 20);
    }
}
```

### 0.2 Base Models para Realtime

#### IDE.Application/Realtime/Models/CollaborationModels.cs
```csharp
namespace IDE.Application.Realtime.Models
{
    public class CollaborationSession
    {
        public Guid Id { get; set; }
        public Guid WorkspaceId { get; set; }
        public Guid UserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsActive { get; set; }
        public string ConnectionId { get; set; }
    }

    public class ItemChangeRecord
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid UserId { get; set; }
        public string ChangeType { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public int Version { get; set; }
        public string Metadata { get; set; }
    }

    public class ItemChange
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Content { get; set; }
        public int Position { get; set; }
        public string Metadata { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ChangeResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ItemChangeRecord ChangeRecord { get; set; }
        public List<ConflictResolution> Conflicts { get; set; }
    }

    public class ConflictResolution
    {
        public Guid ItemId { get; set; }
        public string ResolutionType { get; set; }
        public string ResolvedContent { get; set; }
        public List<ItemChange> ConflictingChanges { get; set; }
        public Guid ResolvedBy { get; set; }
        public DateTime ResolvedAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/Models/PresenceModels.cs
```csharp
namespace IDE.Application.Realtime.Models
{
    public class UserPresence
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public Guid WorkspaceId { get; set; }
        public string Status { get; set; }
        public string ConnectionId { get; set; }
        public DateTime LastSeenAt { get; set; }
        public string CurrentItemId { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class TypingIndicator
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public Guid WorkspaceId { get; set; }
        public string ItemId { get; set; }
        public bool IsTyping { get; set; }
        public DateTime LastTypingAt { get; set; }
    }
}
```

#### IDE.Application/Realtime/Models/ChatModels.cs
```csharp
namespace IDE.Application.Realtime.Models
{
    public enum ChatMessageType
    {
        Text = 1,
        File = 2,
        Image = 3,
        System = 4,
        Code = 5
    }

    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid WorkspaceId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Content { get; set; }
        public ChatMessageType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public List<ChatReaction> Reactions { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class ChatReaction
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Emoji { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

### 0.3 ApplicationDbContext Optimizations

Aplicando as otimizações de performance no DbContext desde o início:

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Enhanced)
```csharp
using Microsoft.EntityFrameworkCore;
using IDE.Domain.Entities;
using IDE.Application.Realtime.Models;

namespace IDE.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets principais
        public DbSet<User> Users { get; set; }
        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<ModuleItem> ModuleItems { get; set; }
        public DbSet<WorkspacePermission> WorkspacePermissions { get; set; }

        // DbSets de colaboração
        public DbSet<CollaborationSession> CollaborationSessions { get; set; }
        public DbSet<ItemChangeRecord> ItemChangeRecords { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatReaction> ChatReactions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                return;

            // Performance optimizations
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableServiceProviderCaching();
            optionsBuilder.EnableDetailedErrors(false);
            
            // Connection optimizations
            optionsBuilder.LogTo(null); // Disable EF logging by default
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Performance indexes
            ConfigurePerformanceIndexes(modelBuilder);
            
            // Entity configurations
            ConfigureCollaborationEntities(modelBuilder);
            ConfigureChatEntities(modelBuilder);
        }

        private void ConfigurePerformanceIndexes(ModelBuilder modelBuilder)
        {
            // Workspace indexes
            modelBuilder.Entity<Workspace>()
                .HasIndex(w => new { w.OwnerId, w.IsDeleted })
                .HasDatabaseName("IX_Workspaces_Owner_NotDeleted");

            // ModuleItem indexes  
            modelBuilder.Entity<ModuleItem>()
                .HasIndex(m => new { m.WorkspaceId, m.IsDeleted })
                .HasDatabaseName("IX_ModuleItems_Workspace_NotDeleted");

            // CollaborationSession indexes
            modelBuilder.Entity<CollaborationSession>()
                .HasIndex(c => new { c.WorkspaceId, c.IsActive })
                .HasDatabaseName("IX_CollaborationSessions_Workspace_Active");

            // ItemChangeRecord indexes
            modelBuilder.Entity<ItemChangeRecord>()
                .HasIndex(i => new { i.ItemId, i.Timestamp })
                .HasDatabaseName("IX_ItemChangeRecords_Item_Timestamp");

            // ChatMessage indexes
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(c => new { c.WorkspaceId, c.CreatedAt })
                .HasDatabaseName("IX_ChatMessages_Workspace_Created");
        }

        private void ConfigureCollaborationEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CollaborationSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ConnectionId).HasMaxLength(100);
                entity.HasIndex(e => e.ConnectionId).IsUnique();
            });

            modelBuilder.Entity<ItemChangeRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChangeType).HasMaxLength(50);
                entity.Property(e => e.Content).HasColumnType("jsonb");
                entity.Property(e => e.Metadata).HasColumnType("jsonb");
            });
        }

        private void ConfigureChatEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).HasMaxLength(4000);
                entity.Property(e => e.Type).HasConversion<int>();
                entity.HasMany(e => e.Reactions)
                      .WithOne()
                      .HasForeignKey(r => r.MessageId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ChatReaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Emoji).HasMaxLength(10);
                entity.HasIndex(e => new { e.MessageId, e.UserId, e.Emoji })
                      .IsUnique()
                      .HasDatabaseName("IX_ChatReactions_Unique");
            });
        }
    }
}
```

### 0.4 Dependency Injection Configuration

#### IDE.API/Extensions/ServiceCollectionExtensions.cs
```csharp
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using IDE.Infrastructure.Data;
using IDE.Infrastructure.Caching;
using IDE.Infrastructure.Services.Collaboration;
using IDE.Infrastructure.Services.Realtime;
using IDE.Infrastructure.Services.Chat;
using IDE.Application.Common.Interfaces;
using IDE.Application.Realtime.Services;

namespace IDE.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFase4Services(this IServiceCollection services, IConfiguration configuration)
        {
            // Database Context com otimizações
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                });

                // Performance optimizations
                options.EnableSensitiveDataLogging(false);
                options.EnableServiceProviderCaching();
                options.EnableDetailedErrors(false);
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });

            // Redis Configuration
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var connectionString = configuration.GetConnectionString("Redis");
                var configuration = ConfigurationOptions.Parse(connectionString, true);
                
                configuration.ConnectRetry = 3;
                configuration.ConnectTimeout = 5000;
                configuration.SyncTimeout = 5000;
                configuration.AbortOnConnectFail = false;
                
                return ConnectionMultiplexer.Connect(configuration);
            });

            // Cache Services
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<IRedisCacheService, RedisCacheService>();
            services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();
            services.AddSingleton<ICachePerformanceMonitor, CachePerformanceMonitor>();
            services.AddSingleton<ICacheWarmupService, CacheWarmupService>();

            // Collaboration Services
            services.AddScoped<ICollaborationService, CollaborationService>();
            services.AddScoped<IUserPresenceService, UserPresenceService>();
            services.AddScoped<IChatService, ChatService>();

            // Performance Services
            services.AddScoped<IPerformanceTracker, PerformanceTracker>();
            services.AddScoped<IActivityLogger, ActivityLogger>();
            services.AddScoped<ISlaMonitoring, SlaMonitoring>();

            // Rate Limiting Services
            services.AddScoped<IPlanBasedRateLimitingService, PlanBasedRateLimitingService>();
            services.AddScoped<ISystemParameterService, SystemParameterService>();

            return services;
        }

        public static IServiceCollection AddFase4Monitoring(this IServiceCollection services, IConfiguration configuration)
        {
            // Application Insights
            services.AddApplicationInsightsTelemetry(configuration);
            
            // Custom telemetry
            services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, UserTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WorkspaceTelemetryInitializer>();
            
            services.AddScoped<ICustomTelemetryClient, CustomTelemetryClient>();
            
            return services;
        }
    }
}
```

---

## 1.1 Substituição do WorkspaceService Mock

O frontend React atualmente utiliza um `WorkspaceService` com dados mock/fake que deve ser completamente substituído por chamadas HTTP reais ao backend .NET Core.

### IDE.Frontend/src/services/WorkspaceService.ts (Adaptação)
```typescript
import { IWorkspace, IModuleItem, IWorkspaceNavigationState } from '@/core/interfaces';

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string;
  statusCode: number;
  timestamp: string;
}

export class WorkspaceService {
  private baseUrl: string;
  private authToken: string | null = null;

  constructor(baseUrl: string = 'http://localhost:8503/api') {
    this.baseUrl = baseUrl;
  }

  setAuthToken(token: string) {
    this.authToken = token;
  }

  private async fetchWithAuth<T>(url: string, options: RequestInit = {}): Promise<ApiResponse<T>> {
    const headers = {
      'Content-Type': 'application/json',
      ...(this.authToken && { 'Authorization': `Bearer ${this.authToken}` }),
      ...options.headers,
    };

    const response = await fetch(`${this.baseUrl}${url}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    return response.json();
  }

  // Workspace CRUD Operations
  async createWorkspace(data: {
    name: string;
    description: string;
    defaultPhases?: string[];
  }): Promise<IWorkspace> {
    const response = await this.fetchWithAuth<IWorkspace>('/workspaces', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return response.data;
  }

  async getWorkspace(id: string): Promise<IWorkspace> {
    const response = await this.fetchWithAuth<IWorkspace>(`/workspaces/${id}`);
    return response.data;
  }

  async getUserWorkspaces(): Promise<IWorkspace[]> {
    const response = await this.fetchWithAuth<IWorkspace[]>('/workspaces');
    return response.data;
  }

  async updateWorkspace(id: string, data: Partial<IWorkspace>): Promise<IWorkspace> {
    const response = await this.fetchWithAuth<IWorkspace>(`/workspaces/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
    return response.data;
  }

  async deleteWorkspace(id: string): Promise<void> {
    await this.fetchWithAuth(`/workspaces/${id}`, {
      method: 'DELETE',
    });
  }

  // Module Items Operations
  async createModuleItem(workspaceId: string, data: {
    name: string;
    content: string;
    module: string;
    type: string;
    parentId?: string;
    tags?: string[];
  }): Promise<IModuleItem> {
    const response = await this.fetchWithAuth<IModuleItem>(`/workspaces/${workspaceId}/items`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return response.data;
  }

  async getModuleItems(workspaceId: string): Promise<IModuleItem[]> {
    const response = await this.fetchWithAuth<IModuleItem[]>(`/workspaces/${workspaceId}/items`);
    return response.data;
  }

  async updateModuleItem(workspaceId: string, itemId: string, data: Partial<IModuleItem>): Promise<IModuleItem> {
    const response = await this.fetchWithAuth<IModuleItem>(`/workspaces/${workspaceId}/items/${itemId}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
    return response.data;
  }

  async deleteModuleItem(workspaceId: string, itemId: string): Promise<void> {
    await this.fetchWithAuth(`/workspaces/${workspaceId}/items/${itemId}`, {
      method: 'DELETE',
    });
  }

  // Navigation State Operations
  async getNavigationState(workspaceId: string, module: string): Promise<IWorkspaceNavigationState> {
    const response = await this.fetchWithAuth<IWorkspaceNavigationState>(
      `/workspaces/${workspaceId}/navigation/${module}`
    );
    return response.data;
  }

  async setNavigationState(workspaceId: string, module: string, state: IWorkspaceNavigationState): Promise<void> {
    await this.fetchWithAuth(`/workspaces/${workspaceId}/navigation/${module}`, {
      method: 'PUT',
      body: JSON.stringify(state),
    });
  }
}

// Export singleton instance
export const workspaceService = new WorkspaceService();
```

### IDE.Frontend/src/hooks/useWorkspace.ts (Adaptação)
```typescript
import { useContext, useCallback, useState } from 'react';
import { WorkspaceContext } from '@/contexts/WorkspaceContext';
import { workspaceService } from '@/services/WorkspaceService';
import { useAuth } from './useAuth';
import type { IWorkspace, IModuleItem } from '@/core/interfaces';

export const useWorkspace = () => {
  const context = useContext(WorkspaceContext);
  const { token } = useAuth();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!context) {
    throw new Error('useWorkspace must be used within a WorkspaceProvider');
  }

  // Set auth token when available
  if (token) {
    workspaceService.setAuthToken(token);
  }

  const createWorkspace = useCallback(async (data: {
    name: string;
    description: string;
    defaultPhases?: string[];
  }): Promise<IWorkspace | null> => {
    try {
      setLoading(true);
      setError(null);
      const workspace = await workspaceService.createWorkspace(data);
      context.addWorkspace(workspace);
      return workspace;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create workspace');
      return null;
    } finally {
      setLoading(false);
    }
  }, [context]);

  const loadWorkspaces = useCallback(async (): Promise<void> => {
    try {
      setLoading(true);
      setError(null);
      const workspaces = await workspaceService.getUserWorkspaces();
      context.setWorkspaces(workspaces);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load workspaces');
    } finally {
      setLoading(false);
    }
  }, [context]);

  const createItem = useCallback(async (data: {
    name: string;
    content: string;
    module: string;
    type: string;
    parentId?: string;
    tags?: string[];
  }): Promise<IModuleItem | null> => {
    if (!context.activeWorkspace) {
      setError('No active workspace');
      return null;
    }

    try {
      setLoading(true);
      setError(null);
      const item = await workspaceService.createModuleItem(context.activeWorkspace.id, data);
      context.addItem(item);
      return item;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create item');
      return null;
    } finally {
      setLoading(false);
    }
  }, [context]);

  return {
    ...context,
    createWorkspace,
    loadWorkspaces,
    createItem,
    loading,
    error,
  };
};
```

---

## 1.2 SignalR Real-time Integration

Implementação de conectividade SignalR para colaboração em tempo real entre o frontend React e backend .NET Core.

### IDE.Frontend/src/services/SignalRService.ts
```typescript
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useAuth } from '@/hooks/useAuth';

export interface WorkspaceEvent {
  type: 'item_updated' | 'item_created' | 'item_deleted' | 'user_joined' | 'user_left' | 'typing_start' | 'typing_stop';
  workspaceId: string;
  userId: string;
  username: string;
  data: any;
  timestamp: string;
}

export interface ChatMessage {
  id: string;
  content: string;
  userId: string;
  username: string;
  timestamp: string;
  workspaceId: string;
}

export interface UserPresence {
  userId: string;
  username: string;
  status: 'online' | 'away' | 'busy';
  currentItem?: string;
  lastSeen: string;
}

export class SignalRService {
  private connection: HubConnection | null = null;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectInterval = 5000;

  constructor(private baseUrl: string = 'http://localhost:8503') {}

  async connect(token: string): Promise<void> {
    if (this.connection?.state === 'Connected') return;

    this.connection = new HubConnectionBuilder()
      .withUrl(`${this.baseUrl}/hubs/workspace`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount < this.maxReconnectAttempts) {
            return this.reconnectInterval;
          }
          return null; // Stop reconnecting
        },
      })
      .configureLogging(LogLevel.Information)
      .build();

    // Connection event handlers
    this.connection.onreconnecting(() => {
      console.log('SignalR: Reconnecting...');
    });

    this.connection.onreconnected(() => {
      console.log('SignalR: Reconnected');
      this.reconnectAttempts = 0;
    });

    this.connection.onclose(() => {
      console.log('SignalR: Connection closed');
    });

    await this.connection.start();
    console.log('SignalR: Connected');
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  // Workspace Events
  async joinWorkspace(workspaceId: string): Promise<void> {
    if (this.connection?.state === 'Connected') {
      await this.connection.invoke('JoinWorkspace', workspaceId);
    }
  }

  async leaveWorkspace(workspaceId: string): Promise<void> {
    if (this.connection?.state === 'Connected') {
      await this.connection.invoke('LeaveWorkspace', workspaceId);
    }
  }

  onWorkspaceEvent(callback: (event: WorkspaceEvent) => void): () => void {
    if (!this.connection) return () => {};

    this.connection.on('WorkspaceEvent', callback);
    return () => this.connection?.off('WorkspaceEvent', callback);
  }

  // Chat
  async sendChatMessage(workspaceId: string, content: string): Promise<void> {
    if (this.connection?.state === 'Connected') {
      await this.connection.invoke('SendChatMessage', workspaceId, content);
    }
  }

  onChatMessage(callback: (message: ChatMessage) => void): () => void {
    if (!this.connection) return () => {};

    this.connection.on('ChatMessage', callback);
    return () => this.connection?.off('ChatMessage', callback);
  }

  // Typing Indicators
  async startTyping(workspaceId: string, itemId?: string): Promise<void> {
    if (this.connection?.state === 'Connected') {
      await this.connection.invoke('StartTyping', workspaceId, itemId);
    }
  }

  async stopTyping(workspaceId: string, itemId?: string): Promise<void> {
    if (this.connection?.state === 'Connected') {
      await this.connection.invoke('StopTyping', workspaceId, itemId);
    }
  }

  // User Presence
  onUserPresenceUpdated(callback: (presence: UserPresence[]) => void): () => void {
    if (!this.connection) return () => {};

    this.connection.on('UserPresenceUpdated', callback);
    return () => this.connection?.off('UserPresenceUpdated', callback);
  }

  // Item Synchronization
  async notifyItemChange(workspaceId: string, itemId: string, changeType: 'update' | 'create' | 'delete'): Promise<void> {
    if (this.connection?.state === 'Connected') {
      await this.connection.invoke('NotifyItemChange', workspaceId, itemId, changeType);
    }
  }
}

// Export singleton instance
export const signalRService = new SignalRService();
```

### IDE.Frontend/src/hooks/useSignalR.ts
```typescript
import { useEffect, useCallback, useState } from 'react';
import { signalRService, WorkspaceEvent, ChatMessage, UserPresence } from '@/services/SignalRService';
import { useAuth } from './useAuth';
import { useWorkspace } from './useWorkspace';

export const useSignalR = () => {
  const { token, user } = useAuth();
  const { activeWorkspace } = useWorkspace();
  const [isConnected, setIsConnected] = useState(false);
  const [workspaceEvents, setWorkspaceEvents] = useState<WorkspaceEvent[]>([]);
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
  const [userPresence, setUserPresence] = useState<UserPresence[]>([]);
  const [typingUsers, setTypingUsers] = useState<Set<string>>(new Set());

  // Connect to SignalR when token is available
  useEffect(() => {
    if (token && user) {
      const connect = async () => {
        try {
          await signalRService.connect(token);
          setIsConnected(true);
        } catch (error) {
          console.error('Failed to connect to SignalR:', error);
          setIsConnected(false);
        }
      };

      connect();

      return () => {
        signalRService.disconnect();
        setIsConnected(false);
      };
    }
  }, [token, user]);

  // Join/leave workspace when active workspace changes
  useEffect(() => {
    if (isConnected && activeWorkspace) {
      signalRService.joinWorkspace(activeWorkspace.id);

      return () => {
        if (activeWorkspace) {
          signalRService.leaveWorkspace(activeWorkspace.id);
        }
      };
    }
  }, [isConnected, activeWorkspace]);

  // Subscribe to workspace events
  useEffect(() => {
    if (!isConnected) return;

    const unsubscribeWorkspaceEvents = signalRService.onWorkspaceEvent((event) => {
      setWorkspaceEvents(prev => [...prev.slice(-99), event]); // Keep last 100 events

      // Handle typing indicators
      if (event.type === 'typing_start') {
        setTypingUsers(prev => new Set([...prev, event.userId]));
      } else if (event.type === 'typing_stop') {
        setTypingUsers(prev => {
          const newSet = new Set(prev);
          newSet.delete(event.userId);
          return newSet;
        });
      }
    });

    const unsubscribeChatMessages = signalRService.onChatMessage((message) => {
      setChatMessages(prev => [...prev, message]);
    });

    const unsubscribeUserPresence = signalRService.onUserPresenceUpdated((presence) => {
      setUserPresence(presence);
    });

    return () => {
      unsubscribeWorkspaceEvents();
      unsubscribeChatMessages();
      unsubscribeUserPresence();
    };
  }, [isConnected]);

  const sendChatMessage = useCallback(async (content: string) => {
    if (activeWorkspace && isConnected) {
      await signalRService.sendChatMessage(activeWorkspace.id, content);
    }
  }, [activeWorkspace, isConnected]);

  const startTyping = useCallback(async (itemId?: string) => {
    if (activeWorkspace && isConnected) {
      await signalRService.startTyping(activeWorkspace.id, itemId);
    }
  }, [activeWorkspace, isConnected]);

  const stopTyping = useCallback(async (itemId?: string) => {
    if (activeWorkspace && isConnected) {
      await signalRService.stopTyping(activeWorkspace.id, itemId);
    }
  }, [activeWorkspace, isConnected]);

  return {
    isConnected,
    workspaceEvents,
    chatMessages,
    userPresence,
    typingUsers: Array.from(typingUsers),
    sendChatMessage,
    startTyping,
    stopTyping,
  };
};
```

---

## 1.3 Authentication Integration

Integração completa do sistema de autenticação JWT + OAuth com o backend .NET Core.

### IDE.Frontend/src/services/authService.ts (Adaptação)
```typescript
import { User } from '@/contexts/AuthContext';

interface LoginRequest {
  email: string;
  password: string;
}

interface RegisterRequest {
  email: string;
  username: string;
  password: string;
  firstName: string;
  lastName: string;
}

interface AuthResponse {
  user: User;
  token: string;
  refreshToken: string;
  expiresAt: string;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string;
  statusCode: number;
  timestamp: string;
}

class AuthService {
  private baseUrl: string;
  private refreshToken: string | null = null;

  constructor(baseUrl: string = 'http://localhost:8503/api/auth') {
    this.baseUrl = baseUrl;
    this.refreshToken = localStorage.getItem('refreshToken');
  }

  async login(credentials: LoginRequest): Promise<AuthResponse> {
    const response = await fetch(`${this.baseUrl}/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(credentials),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Login failed');
    }

    const apiResponse: ApiResponse<AuthResponse> = await response.json();
    const authData = apiResponse.data;

    // Store refresh token
    localStorage.setItem('refreshToken', authData.refreshToken);
    this.refreshToken = authData.refreshToken;

    return authData;
  }

  async register(userData: RegisterRequest): Promise<AuthResponse> {
    const response = await fetch(`${this.baseUrl}/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(userData),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Registration failed');
    }

    const apiResponse: ApiResponse<AuthResponse> = await response.json();
    const authData = apiResponse.data;

    // Store refresh token
    localStorage.setItem('refreshToken', authData.refreshToken);
    this.refreshToken = authData.refreshToken;

    return authData;
  }

  async refreshAccessToken(): Promise<string | null> {
    if (!this.refreshToken) return null;

    try {
      const response = await fetch(`${this.baseUrl}/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ refreshToken: this.refreshToken }),
      });

      if (!response.ok) {
        this.logout();
        return null;
      }

      const apiResponse: ApiResponse<{ token: string; expiresAt: string }> = await response.json();
      return apiResponse.data.token;
    } catch (error) {
      this.logout();
      return null;
    }
  }

  async getCurrentUser(token: string): Promise<User> {
    const response = await fetch(`${this.baseUrl}/me`, {
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error('Failed to get current user');
    }

    const apiResponse: ApiResponse<User> = await response.json();
    return apiResponse.data;
  }

  async logout(): Promise<void> {
    if (this.refreshToken) {
      try {
        await fetch(`${this.baseUrl}/logout`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ refreshToken: this.refreshToken }),
        });
      } catch (error) {
        console.error('Logout error:', error);
      }
    }

    localStorage.removeItem('refreshToken');
    this.refreshToken = null;
  }

  // OAuth Methods
  getOAuthUrl(provider: 'github' | 'google' | 'microsoft'): string {
    return `${this.baseUrl}/oauth/${provider}`;
  }

  async handleOAuthCallback(provider: string, code: string, state?: string): Promise<AuthResponse> {
    const response = await fetch(`${this.baseUrl}/oauth/${provider}/callback`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ code, state }),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'OAuth authentication failed');
    }

    const apiResponse: ApiResponse<AuthResponse> = await response.json();
    const authData = apiResponse.data;

    // Store refresh token
    localStorage.setItem('refreshToken', authData.refreshToken);
    this.refreshToken = authData.refreshToken;

    return authData;
  }
}

export const authService = new AuthService();
export type { LoginRequest, RegisterRequest, AuthResponse };
```

---

## Entregáveis da Parte 1

### ✅ Implementações Completas
- **WorkspaceService.ts** adaptado para APIs REST reais
- **useWorkspace.ts** hook integrado com backend
- **SignalRService.ts** para conexões em tempo real
- **useSignalR.ts** hook para gerenciar eventos SignalR
- **authService.ts** com JWT + OAuth completo

### ✅ Funcionalidades Integradas
- **CRUD de Workspaces** via API REST
- **Gerenciamento de ModuleItems** via API REST
- **Estados de navegação** persistidos no backend
- **Conexão SignalR** com reconexão automática
- **Autenticação JWT** com refresh token
- **OAuth providers** (GitHub, Google, Microsoft)

### ✅ Features de Tempo Real
- **Eventos de workspace** (join/leave/typing)
- **Chat messages** em tempo real
- **User presence** e status
- **Typing indicators** por item
- **Item synchronization** entre usuários

---

## Validação da Parte 1

### Critérios de Sucesso
- [ ] WorkspaceService conecta com backend sem erros
- [ ] CRUD de workspaces funciona via API
- [ ] SignalR conecta e recebe eventos
- [ ] Autenticação JWT funciona end-to-end
- [ ] OAuth providers redirecionam corretamente
- [ ] Hooks React mantêm compatibilidade
- [ ] Contextos React continuam funcionando
- [ ] Não há breaking changes no frontend

### Testes Manuais
```bash
# 1. Verificar conexão com backend
curl http://localhost:8503/api/workspaces

# 2. Testar autenticação
curl -X POST http://localhost:8503/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'

# 3. Verificar SignalR endpoint
curl http://localhost:8503/hubs/workspace
```

### Próximos Passos
Após validação da Parte 1, prosseguir para:
- **Parte 2**: Real-time Collaboration Core
- **Parte 3**: User Presence & Chat System

---

**Tempo Estimado**: 4-6 horas  
**Complexidade**: Média  
**Dependências**: Backend Fases 1-3 funcionais  
**Entregável**: Frontend totalmente integrado com backend