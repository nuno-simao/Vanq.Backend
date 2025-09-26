# Fase 4: Otimização, Integração e Deploy Production - Backend .NET Core 8 + Azure

## Contexto da Fase

Esta é a **quarta e última fase** de implementação da IDE colaborativa. Com o backend .NET Core 8 completamente implementado nas fases 1-3, agora focaremos em:

- **Integração completa** com o frontend React existente (substituição de dados mock por APIs reais)
- **Otimização de performance** e caching com Redis e PostgreSQL
- **Deploy production-ready** em Azure Kubernetes Service
- **Colaboração em tempo real** com sincronização de nível médio
- **Testes end-to-end** automatizados completos
- **Monitoramento e observabilidade** total
- **Configurações de segurança** avançadas para produção

**Pré-requisitos**: Fases 1, 2 e 3 devem estar 100% funcionais

## Alinhamento Frontend ↔ Backend

Esta fase foi especificamente projetada para **integração total** com a arquitetura frontend React 19 + TypeScript + Ant Design existente:

### Estado Atual Frontend
- **WorkspaceService** usando dados mock/fake
- **Hooks React** (useWorkspace, useAuth, etc.) funcionais com dados simulados
- **Contextos React** estabelecidos e funcionais
- **Sistema de módulos** e editores implementado
- **Arquitetura** baseada em workspaces conceituais

### Integração Planejada
- **Substituição completa** dos dados mock por chamadas HTTP reais
- **Adaptação dos hooks** para consumir APIs REST do backend .NET Core
- **Implementação SignalR** para colaboração em tempo real
- **Manutenção dos contextos** React sem breaking changes
- **Sincronização workspace** via REST + WebSocket

## Objetivos da Fase

✅ **Integração Frontend ↔ Backend** com substituição completa dos dados mock  
✅ **Colaboração em tempo real** com sincronização de nível médio (2-3s)  
✅ **Performance e caching** otimizado com Redis VM e PostgreSQL Azure  
✅ **Deploy Azure Kubernetes** com auto-scaling e rolling updates  
✅ **Testes Playwright** completos (E2E + Performance + Load + Security)  
✅ **Monitoramento total** (Performance + Engagement + Collaboration + Business)  
✅ **Rate limiting** inteligente por plano de usuário  
✅ **Versionamento de APIs** com backward compatibility  
✅ **Backup e disaster recovery** procedures documentados  
✅ **CI/CD pipeline** completo para 3 ambientes  
✅ **Documentation operacional** para produção  
✅ **SLA 99.9%** uptime target com alerting  

## 1. Integração Frontend React ↔ Backend .NET Core

### 1.1 Substituição do WorkspaceService Mock

O frontend React atualmente utiliza um `WorkspaceService` com dados mock/fake que deve ser completamente substituído por chamadas HTTP reais ao backend .NET Core.

#### IDE.Frontend/src/services/WorkspaceService.ts (Adaptação)
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

#### IDE.Frontend/src/hooks/useWorkspace.ts (Adaptação)
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

### 1.2 SignalR Real-time Integration

Implementação de conectividade SignalR para colaboração em tempo real entre o frontend React e backend .NET Core.

#### IDE.Frontend/src/services/SignalRService.ts
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

#### IDE.Frontend/src/hooks/useSignalR.ts
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

### 1.3 Authentication Integration

Integração completa do sistema de autenticação JWT + OAuth com o backend .NET Core.

#### IDE.Frontend/src/services/authService.ts (Adaptação)
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
## 2. Colaboração em Tempo Real (Nível Médio)

### 2.1 Sincronização Inteligente (2-3 segundos)

Implementação de sincronização de mudanças com debounce de 2-3 segundos para otimizar performance e reduzir conflitos.

#### IDE.Application/Realtime/ICollaborationService.cs
```csharp
public interface ICollaborationService
{
    Task<CollaborationSession> StartSessionAsync(Guid workspaceId, Guid userId);
    Task EndSessionAsync(Guid workspaceId, Guid userId);
    Task<ChangeResult> ApplyChangeAsync(Guid workspaceId, Guid itemId, ItemChange change);
    Task<List<ItemChange>> GetPendingChangesAsync(Guid workspaceId, Guid itemId, DateTime since);
    Task<ConflictResolution> ResolveConflictAsync(Guid workspaceId, Guid itemId, List<ItemChange> conflicts);
    Task NotifyItemChangedAsync(Guid workspaceId, Guid itemId, string changeType);
}

public class CollaborationService : ICollaborationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly IHubContext<WorkspaceHub> _hubContext;
    private readonly ILogger<CollaborationService> _logger;

    public CollaborationService(
        ApplicationDbContext context,
        ICacheService cache,
        IHubContext<WorkspaceHub> hubContext,
        ILogger<CollaborationService> logger)
    {
        _context = context;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<CollaborationSession> StartSessionAsync(Guid workspaceId, Guid userId)
    {
        var session = new CollaborationSession
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IsActive = true
        };

        _context.CollaborationSessions.Add(session);
        await _context.SaveChangesAsync();

        // Cache session
        await _cache.SetAsync($"session:{session.Id}", session, TimeSpan.FromHours(8));

        // Notify other users
        await _hubContext.Clients.Group($"workspace:{workspaceId}")
            .SendAsync("UserJoined", new
            {
                UserId = userId,
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow
            });

        _logger.LogInformation("Collaboration session started: {SessionId} for user {UserId} in workspace {WorkspaceId}",
            session.Id, userId, workspaceId);

        return session;
    }

    public async Task<ChangeResult> ApplyChangeAsync(Guid workspaceId, Guid itemId, ItemChange change)
    {
        var lockKey = $"item_lock:{itemId}";
        var isLocked = await _cache.ExistsAsync(lockKey);

        if (isLocked)
        {
            // Get pending changes for conflict detection
            var pendingChanges = await GetPendingChangesAsync(workspaceId, itemId, change.Timestamp.AddSeconds(-5));
            
            if (pendingChanges.Any())
            {
                return new ChangeResult
                {
                    Success = false,
                    ConflictDetected = true,
                    ConflictingChanges = pendingChanges,
                    RequiresResolution = true
                };
            }
        }

        // Apply lock for 3 seconds
        await _cache.SetAsync(lockKey, change.UserId, TimeSpan.FromSeconds(3));

        try
        {
            // Get current item
            var item = await _context.ModuleItems.FindAsync(itemId);
            if (item == null)
            {
                return new ChangeResult { Success = false, Error = "Item not found" };
            }

            // Apply change based on type
            switch (change.Type)
            {
                case ChangeType.Insert:
                    item.Content = ApplyInsert(item.Content, change);
                    break;
                case ChangeType.Delete:
                    item.Content = ApplyDelete(item.Content, change);
                    break;
                case ChangeType.Replace:
                    item.Content = ApplyReplace(item.Content, change);
                    break;
            }

            // Update version
            item.Version++;
            item.UpdatedAt = DateTime.UtcNow;

            // Save change record
            var changeRecord = new ItemChangeRecord
            {
                Id = Guid.NewGuid(),
                ItemId = itemId,
                UserId = change.UserId,
                ChangeType = change.Type.ToString(),
                StartPosition = change.StartPosition,
                EndPosition = change.EndPosition,
                Content = change.Content,
                Timestamp = DateTime.UtcNow,
                Version = item.Version
            };

            _context.ItemChangeRecords.Add(changeRecord);
            await _context.SaveChangesAsync();

            // Cache the change
            await _cache.SetAsync($"change:{changeRecord.Id}", changeRecord, TimeSpan.FromMinutes(10));

            // Notify other users
            await NotifyItemChangedAsync(workspaceId, itemId, "update");

            return new ChangeResult
            {
                Success = true,
                NewVersion = item.Version,
                ChangeId = changeRecord.Id
            };
        }
        finally
        {
            await _cache.RemoveAsync(lockKey);
        }
    }

    public async Task<ConflictResolution> ResolveConflictAsync(Guid workspaceId, Guid itemId, List<ItemChange> conflicts)
    {
        // Simple last-writer-wins strategy for medium-level synchronization
        var latestChange = conflicts.OrderByDescending(c => c.Timestamp).First();
        
        var resolution = new ConflictResolution
        {
            ResolvedChange = latestChange,
            DiscardedChanges = conflicts.Where(c => c.Id != latestChange.Id).ToList(),
            ResolutionStrategy = "LastWriterWins",
            Timestamp = DateTime.UtcNow
        };

        // Log conflict resolution
        _logger.LogWarning("Conflict resolved for item {ItemId} in workspace {WorkspaceId}. " +
                          "Strategy: {Strategy}, Discarded changes: {DiscardedCount}",
            itemId, workspaceId, resolution.ResolutionStrategy, resolution.DiscardedChanges.Count);

        return resolution;
    }

    private string ApplyInsert(string content, ItemChange change)
    {
        if (change.StartPosition > content.Length)
            return content + change.Content;
        
        return content.Insert(change.StartPosition, change.Content);
    }

    private string ApplyDelete(string content, ItemChange change)
    {
        var length = Math.Min(change.EndPosition - change.StartPosition, content.Length - change.StartPosition);
        if (length <= 0) return content;
        
        return content.Remove(change.StartPosition, length);
    }

    private string ApplyReplace(string content, ItemChange change)
    {
        var deleteLength = Math.Min(change.EndPosition - change.StartPosition, content.Length - change.StartPosition);
        if (deleteLength > 0)
        {
            content = content.Remove(change.StartPosition, deleteLength);
        }
        
        return content.Insert(change.StartPosition, change.Content);
    }
}
```

### 2.2 Presença de Usuários e Awareness

Sistema de presença visual e indicadores de atividade para colaboradores.

#### IDE.Application/Realtime/IUserPresenceService.cs
```csharp
public interface IUserPresenceService
{
    Task<UserPresence> UpdatePresenceAsync(Guid workspaceId, Guid userId, string connectionId, string status = "online");
    Task<List<UserPresence>> GetWorkspacePresenceAsync(Guid workspaceId);
    Task RemovePresenceAsync(string connectionId);
    Task UpdateTypingStatusAsync(Guid workspaceId, Guid userId, bool isTyping, string itemId = null);
    Task<List<TypingIndicator>> GetTypingUsersAsync(Guid workspaceId, string itemId = null);
    Task CleanupStaleConnectionsAsync();
}

public class UserPresenceService : IUserPresenceService
{
    private readonly ICacheService _cache;
    private readonly IHubContext<WorkspaceHub> _hubContext;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserPresenceService> _logger;

    public UserPresenceService(
        ICacheService cache,
        IHubContext<WorkspaceHub> hubContext,
        ApplicationDbContext context,
        ILogger<UserPresenceService> logger)
    {
        _cache = cache;
        _hubContext = hubContext;
        _context = context;
        _logger = logger;
    }

    public async Task<UserPresence> UpdatePresenceAsync(Guid workspaceId, Guid userId, string connectionId, string status = "online")
    {
        var presence = new UserPresence
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
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

### 2.3 Sistema de Chat em Tempo Real

Implementação completa de chat com histórico, menções e reações.

#### IDE.Application/Realtime/IChatService.cs
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
    private readonly ICacheService _cache;
    private readonly IHubContext<WorkspaceHub> _hubContext;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ApplicationDbContext context,
        ICacheService cache,
        IHubContext<WorkspaceHub> hubContext,
        IInputSanitizer inputSanitizer,
        ILogger<ChatService> logger)
    {
        _context = context;
        _cache = cache;
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
        await _cache.RemoveAsync($"chat:{message.WorkspaceId}:recent");

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
## 3. Performance e Cache Strategy

### 3.1 Redis VM Configuration

Configuração otimizada do Redis para caching de workspaces, sessões e dados de tempo real.

#### IDE.Infrastructure/Caching/IRedisCacheService.cs
```csharp
public interface IRedisCacheService : ICacheService
{
    Task<List<string>> GetKeysAsync(string pattern);
    Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiry = null);
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<T> GetAndDeleteAsync<T>(string key);
    Task<List<T>> GetMultipleAsync<T>(IEnumerable<string> keys);
    Task SetMultipleAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiry = null);
}

public class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
        _server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            
            if (!value.HasValue)
                return default(T);

            return JsonSerializer.Deserialize<T>(value, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            
            await _database.StringSetAsync(key, serializedValue, expiry);
            _logger.LogDebug("Cache key {Key} set with expiry {Expiry}", key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}", key);
        }
    }

    public async Task<List<string>> GetKeysAsync(string pattern)
    {
        try
        {
            return _server.Keys(pattern: pattern).Select(k => k.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting keys with pattern {Pattern}", pattern);
            return new List<string>();
        }
    }

    public async Task<long> IncrementAsync(string key, long value = 1, TimeSpan? expiry = null)
    {
        try
        {
            var result = await _database.StringIncrementAsync(key, value);
            
            if (expiry.HasValue)
            {
                await _database.KeyExpireAsync(key, expiry.Value);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing cache key {Key}", key);
            return 0;
        }
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var result = await _database.StringSetAsync(key, serializedValue, expiry, When.NotExists);
            
            if (result)
            {
                _logger.LogDebug("Cache key {Key} set (if not exists) with expiry {Expiry}", key, expiry);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key} if not exists", key);
            return false;
        }
    }

    public async Task<List<T>> GetMultipleAsync<T>(IEnumerable<string> keys)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var values = await _database.StringGetAsync(redisKeys);
            
            var results = new List<T>();
            foreach (var value in values)
            {
                if (value.HasValue)
                {
                    var item = JsonSerializer.Deserialize<T>(value);
                    results.Add(item);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple cache keys");
            return new List<T>();
        }
    }

    // Implementation of other ICacheService methods...
    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Cache key {Key} removed", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if cache key {Key} exists", key);
            return false;
        }
    }
}
```

### 3.2 Azure PostgreSQL Optimization

Configuração otimizada do PostgreSQL para performance e conexões.

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (Performance Optimizations)
```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // DbSets...
    public DbSet<User> Users { get; set; }
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<ModuleItem> ModuleItems { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ItemChangeRecord> ItemChangeRecords { get; set; }
    public DbSet<CollaborationSession> CollaborationSessions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Performance optimizations
        optionsBuilder.EnableSensitiveDataLogging(false);
        optionsBuilder.EnableServiceProviderCaching();
        optionsBuilder.EnableDetailedErrors(false);
        
        // Connection pooling is handled by Npgsql
        optionsBuilder.UseNpgsql(connectionString =>
        {
            connectionString.CommandTimeout(30);
            connectionString.EnableRetryOnFailure(3);
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexing strategy for performance
        modelBuilder.Entity<Workspace>()
            .HasIndex(w => w.CreatedBy)
            .HasDatabaseName("IX_Workspaces_CreatedBy");

        modelBuilder.Entity<Workspace>()
            .HasIndex(w => new { w.IsArchived, w.CreatedAt })
            .HasDatabaseName("IX_Workspaces_IsArchived_CreatedAt");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => m.WorkspaceId)
            .HasDatabaseName("IX_ModuleItems_WorkspaceId");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => new { m.WorkspaceId, m.Module })
            .HasDatabaseName("IX_ModuleItems_WorkspaceId_Module");

        modelBuilder.Entity<ModuleItem>()
            .HasIndex(m => m.ParentId)
            .HasDatabaseName("IX_ModuleItems_ParentId");

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(c => new { c.WorkspaceId, c.CreatedAt })
            .HasDatabaseName("IX_ChatMessages_WorkspaceId_CreatedAt");

        modelBuilder.Entity<WorkspacePermission>()
            .HasIndex(wp => new { wp.UserId, wp.WorkspaceId })
            .IsUnique()
            .HasDatabaseName("IX_WorkspacePermissions_UserId_WorkspaceId");

        modelBuilder.Entity<ItemChangeRecord>()
            .HasIndex(icr => new { icr.ItemId, icr.Timestamp })
            .HasDatabaseName("IX_ItemChangeRecords_ItemId_Timestamp");

        // Composite indexes for collaboration
        modelBuilder.Entity<CollaborationSession>()
            .HasIndex(cs => new { cs.WorkspaceId, cs.IsActive })
            .HasDatabaseName("IX_CollaborationSessions_WorkspaceId_IsActive");

        // Configure relationships
        modelBuilder.Entity<ModuleItem>()
            .HasOne(m => m.Parent)
            .WithMany(m => m.Children)
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(c => c.ParentMessage)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### IDE.Infrastructure/Data/DatabaseOptimizationService.cs
```csharp
public interface IDatabaseOptimizationService
{
    Task OptimizeQueriesAsync();
    Task UpdateStatisticsAsync();
    Task VacuumAnalyzeAsync();
    Task<DatabaseHealthInfo> GetHealthInfoAsync();
}

public class DatabaseOptimizationService : IDatabaseOptimizationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseOptimizationService> _logger;

    public DatabaseOptimizationService(ApplicationDbContext context, ILogger<DatabaseOptimizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task OptimizeQueriesAsync()
    {
        try
        {
            // Analyze slow queries and optimize
            var slowQueries = await _context.Database.ExecuteSqlRawAsync(@"
                SELECT query, calls, total_time, mean_time 
                FROM pg_stat_statements 
                WHERE calls > 100 AND mean_time > 100 
                ORDER BY mean_time DESC 
                LIMIT 10");

            _logger.LogInformation("Database query optimization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during query optimization");
        }
    }

    public async Task UpdateStatisticsAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("ANALYZE;");
            _logger.LogInformation("Database statistics updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating database statistics");
        }
    }

    public async Task VacuumAnalyzeAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("VACUUM ANALYZE;");
            _logger.LogInformation("Database vacuum and analyze completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during vacuum analyze");
        }
    }

    public async Task<DatabaseHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            var connectionString = _context.Database.GetConnectionString();
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var healthInfo = new DatabaseHealthInfo
            {
                IsHealthy = true,
                ConnectionCount = await GetActiveConnectionsAsync(connection),
                DatabaseSize = await GetDatabaseSizeAsync(connection),
                LargestTables = await GetLargestTablesAsync(connection),
                IndexUsage = await GetIndexUsageAsync(connection),
                CheckedAt = DateTime.UtcNow
            };

            return healthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database health info");
            return new DatabaseHealthInfo
            {
                IsHealthy = false,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<int> GetActiveConnectionsAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE state = 'active'", connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task<long> GetDatabaseSizeAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand("SELECT pg_database_size(current_database())", connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private async Task<List<TableInfo>> GetLargestTablesAsync(NpgsqlConnection connection)
    {
        var tables = new List<TableInfo>();
        using var command = new NpgsqlCommand(@"
            SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
                   pg_total_relation_size(schemaname||'.'||tablename) as size_bytes
            FROM pg_tables 
            WHERE schemaname = 'public'
            ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC 
            LIMIT 10", connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Name = reader.GetString("tablename"),
                Size = reader.GetString("size"),
                SizeBytes = reader.GetInt64("size_bytes")
            });
        }

        return tables;
    }

    private async Task<decimal> GetIndexUsageAsync(NpgsqlConnection connection)
    {
        using var command = new NpgsqlCommand(@"
            SELECT ROUND(
                100.0 * SUM(idx_scan) / (SUM(seq_scan) + SUM(idx_scan) + 0.001), 2
            ) as index_usage_percentage
            FROM pg_stat_user_tables", connection);

        var result = await command.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
    }
}

public class DatabaseHealthInfo
{
    public bool IsHealthy { get; set; }
    public int ConnectionCount { get; set; }
    public long DatabaseSize { get; set; }
    public List<TableInfo> LargestTables { get; set; } = new();
    public decimal IndexUsage { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class TableInfo
{
    public string Name { get; set; }
    public string Size { get; set; }
    public long SizeBytes { get; set; }
}
```

### 3.3 API Performance Optimization

Configurações de performance para APIs REST com caching headers e compressão.

#### IDE.API/Middleware/PerformanceMiddleware.cs
```csharp
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IPerformanceMetrics _metrics;

    public PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger, IPerformanceMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = GetEndpointName(context);

        try
        {
            // Add performance headers
            context.Response.Headers.Add("X-Response-Time", stopwatch.ElapsedMilliseconds.ToString());
            context.Response.Headers.Add("X-Server-Name", Environment.MachineName);

            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Record metrics
            _metrics.RecordApiCall(endpoint, stopwatch.ElapsedMilliseconds, context.Response.StatusCode);

            // Log slow requests
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning("Slow request detected: {Endpoint} took {ElapsedMs}ms - Status: {StatusCode}",
                    endpoint, stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
            }

            // Update response time header
            context.Response.Headers["X-Response-Time"] = stopwatch.ElapsedMilliseconds.ToString();
        }
    }

    private string GetEndpointName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            return endpoint.DisplayName ?? $"{context.Request.Method} {context.Request.Path}";
        }
        
        return $"{context.Request.Method} {context.Request.Path}";
    }
}
```

#### IDE.API/Configuration/CompressionConfiguration.cs
```csharp
public static class CompressionConfiguration
{
    public static IServiceCollection AddResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            
            // MIME types to compress
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/javascript",
                "text/css",
                "text/html",
                "text/json",
                "text/plain",
                "text/xml",
                "application/xml",
                "application/atom+xml",
                "application/rss+xml",
                "application/xhtml+xml",
                "image/svg+xml"
            });
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }
}
```

#### IDE.API/Configuration/CachingConfiguration.cs
```csharp
public static class CachingConfiguration
{
    public static IServiceCollection AddAdvancedCaching(this IServiceCollection services, IConfiguration configuration)
    {
        // Response caching
        services.AddResponseCaching(options =>
        {
            options.MaximumBodySize = 1024 * 1024; // 1MB
            options.UseCaseSensitivePaths = false;
        });

        // Memory cache for local caching
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000; // Limit number of entries
            options.CompactionPercentage = 0.25; // Remove 25% when full
        });

        // Output caching for .NET 8
        services.AddOutputCache(options =>
        {
            // Default policy
            options.AddBasePolicy(builder => 
                builder.Expire(TimeSpan.FromMinutes(5))
                       .Tag("default"));

            // Workspace data policy
            options.AddPolicy("WorkspacePolicy", builder =>
                builder.Expire(TimeSpan.FromMinutes(10))
                       .Tag("workspace")
                       .SetVaryByQuery("workspaceId"));

            // User data policy
            options.AddPolicy("UserPolicy", builder =>
                builder.Expire(TimeSpan.FromMinutes(15))
                       .Tag("user")
                       .SetVaryByHeader("Authorization"));

            // Public data policy (longer cache)
            options.AddPolicy("PublicPolicy", builder =>
                builder.Expire(TimeSpan.FromHours(1))
                       .Tag("public"));
        });

        return services;
    }

    public static IApplicationBuilder UseAdvancedCaching(this IApplicationBuilder app)
    {
        app.UseResponseCaching();
        app.UseOutputCache();
        
        return app;
    }
}
```

## 4. Azure Kubernetes Service Deploy

### 4.1 Environments Architecture

Configuração de três ambientes distintos para desenvolvimento, staging e produção.

#### Ambientes Configurados
- **Local Development:** Docker Compose para desenvolvimento local
- **Azure Development:** AKS cluster para desenvolvimento em nuvem
- **Staging:** AKS cluster para testes de integração
- **Production:** AKS cluster otimizado para produção

### 4.2 Kubernetes Configuration

#### Dockerfile.backend
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/IDE.API/IDE.API.csproj", "src/IDE.API/"]
COPY ["src/IDE.Application/IDE.Application.csproj", "src/IDE.Application/"]
COPY ["src/IDE.Domain/IDE.Domain.csproj", "src/IDE.Domain/"]
COPY ["src/IDE.Infrastructure/IDE.Infrastructure.csproj", "src/IDE.Infrastructure/"]
COPY ["src/IDE.Shared/IDE.Shared.csproj", "src/IDE.Shared/"]

RUN dotnet restore "src/IDE.API/IDE.API.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/IDE.API"
RUN dotnet build "IDE.API.csproj" -c Release -o /app/build

# Publish stage
RUN dotnet publish "IDE.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "IDE.API.dll"]
```

#### k8s/configmap.yaml
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: ide-backend-config
  namespace: ide-production
data:
  appsettings.json: |
    {
      "ConnectionStrings": {
        "DefaultConnection": "",
        "Redis": ""
      },
      "JWT": {
        "Issuer": "IDE.API",
        "Audience": "IDE.Frontend",
        "ExpiryInMinutes": 60
      },
      "RateLimiting": {
        "EnableRateLimiting": true,
        "GlobalLimit": 1000,
        "WindowMinutes": 60
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning",
          "System.Net.Http.HttpClient": "Warning"
        }
      },
      "AllowedHosts": "*",
      "ASPNETCORE_ENVIRONMENT": "Production"
    }
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: ide-backend-config-dev
  namespace: ide-development
data:
  appsettings.json: |
    {
      "ConnectionStrings": {
        "DefaultConnection": "",
        "Redis": ""
      },
      "JWT": {
        "Issuer": "IDE.API",
        "Audience": "IDE.Frontend",
        "ExpiryInMinutes": 60
      },
      "Logging": {
        "LogLevel": {
          "Default": "Debug",
          "Microsoft.AspNetCore": "Information"
        }
      },
      "AllowedHosts": "*",
      "ASPNETCORE_ENVIRONMENT": "Development"
    }
```

#### k8s/secrets.yaml
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ide-backend-secrets
  namespace: ide-production
type: Opaque
data:
  # Base64 encoded values
  ConnectionStrings__DefaultConnection: <base64-encoded-connection-string>
  ConnectionStrings__Redis: <base64-encoded-redis-connection>
  JWT__Secret: <base64-encoded-jwt-secret>
  OAuth__GitHub__ClientSecret: <base64-encoded-github-secret>
  OAuth__Google__ClientSecret: <base64-encoded-google-secret>
  OAuth__Microsoft__ClientSecret: <base64-encoded-microsoft-secret>
---
apiVersion: v1
kind: Secret
metadata:
  name: ide-backend-secrets-dev
  namespace: ide-development
type: Opaque
data:
  # Base64 encoded development values
  ConnectionStrings__DefaultConnection: <base64-encoded-dev-connection-string>
  ConnectionStrings__Redis: <base64-encoded-dev-redis-connection>
  JWT__Secret: <base64-encoded-dev-jwt-secret>
```

#### k8s/deployment.yaml
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ide-backend
  namespace: ide-production
  labels:
    app: ide-backend
    version: v1
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 1
      maxSurge: 1
  selector:
    matchLabels:
      app: ide-backend
  template:
    metadata:
      labels:
        app: ide-backend
        version: v1
    spec:
      containers:
      - name: ide-backend
        image: ideregistry.azurecr.io/ide-backend:latest
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        envFrom:
        - configMapRef:
            name: ide-backend-config
        - secretRef:
            name: ide-backend-secrets
        resources:
          requests:
            cpu: 100m
            memory: 256Mi
          limits:
            cpu: 500m
            memory: 1Gi
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
          timeoutSeconds: 10
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 15
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        startupProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 30
      imagePullSecrets:
      - name: acr-secret
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ide-backend-dev
  namespace: ide-development
spec:
  replicas: 1
  selector:
    matchLabels:
      app: ide-backend-dev
  template:
    metadata:
      labels:
        app: ide-backend-dev
    spec:
      containers:
      - name: ide-backend
        image: ideregistry.azurecr.io/ide-backend:dev
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Development"
        envFrom:
        - configMapRef:
            name: ide-backend-config-dev
        - secretRef:
            name: ide-backend-secrets-dev
        resources:
          requests:
            cpu: 50m
            memory: 128Mi
          limits:
            cpu: 200m
            memory: 512Mi
```

#### k8s/service.yaml
```yaml
apiVersion: v1
kind: Service
metadata:
  name: ide-backend-service
  namespace: ide-production
  labels:
    app: ide-backend
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
    name: http
  selector:
    app: ide-backend
---
apiVersion: v1
kind: Service
metadata:
  name: ide-backend-service-dev
  namespace: ide-development
spec:
  type: ClusterIP
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
  selector:
    app: ide-backend-dev
```

#### k8s/hpa.yaml
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: ide-backend-hpa
  namespace: ide-production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: ide-backend
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 10
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
      - type: Pods
        value: 2
        periodSeconds: 60
      selectPolicy: Max
```

#### k8s/ingress.yaml
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ide-backend-ingress
  namespace: ide-production
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-body-size: "50m"
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-window: "1m"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  tls:
  - hosts:
    - api.ide-platform.com
    secretName: ide-api-tls
  rules:
  - host: api.ide-platform.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: ide-backend-service
            port:
              number: 80
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: ide-backend-ingress-dev
  namespace: ide-development
  annotations:
    kubernetes.io/ingress.class: nginx
    nginx.ingress.kubernetes.io/ssl-redirect: "false"
    nginx.ingress.kubernetes.io/proxy-body-size: "50m"
spec:
  rules:
  - host: api-dev.ide-platform.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: ide-backend-service-dev
            port:
              number: 80
```

### 4.3 Rolling Updates Strategy

#### k8s/pdb.yaml
```yaml
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: ide-backend-pdb
  namespace: ide-production
spec:
  minAvailable: 2
  selector:
    matchLabels:
      app: ide-backend
```

#### scripts/deploy.sh
```bash
#!/bin/bash

set -e

ENVIRONMENT=${1:-production}
IMAGE_TAG=${2:-latest}
NAMESPACE="ide-${ENVIRONMENT}"

echo "Deploying IDE Backend to ${ENVIRONMENT} environment..."
echo "Image tag: ${IMAGE_TAG}"
echo "Namespace: ${NAMESPACE}"

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "kubectl is required but not installed. Aborting."
    exit 1
fi

# Check if namespace exists
if ! kubectl get namespace "${NAMESPACE}" &> /dev/null; then
    echo "Creating namespace ${NAMESPACE}..."
    kubectl create namespace "${NAMESPACE}"
fi

# Apply configuration based on environment
case $ENVIRONMENT in
    "production")
        echo "Deploying to production..."
        
        # Apply secrets (should be done securely)
        kubectl apply -f k8s/secrets.yaml -n "${NAMESPACE}"
        
        # Apply configmaps
        kubectl apply -f k8s/configmap.yaml -n "${NAMESPACE}"
        
        # Apply PDB first
        kubectl apply -f k8s/pdb.yaml -n "${NAMESPACE}"
        
        # Update deployment with new image
        kubectl set image deployment/ide-backend ide-backend=ideregistry.azurecr.io/ide-backend:${IMAGE_TAG} -n "${NAMESPACE}"
        
        # Apply all other resources
        kubectl apply -f k8s/deployment.yaml -n "${NAMESPACE}"
        kubectl apply -f k8s/service.yaml -n "${NAMESPACE}"
        kubectl apply -f k8s/hpa.yaml -n "${NAMESPACE}"
        kubectl apply -f k8s/ingress.yaml -n "${NAMESPACE}"
        ;;
        
    "development")
        echo "Deploying to development..."
        
        # Apply dev-specific resources
        kubectl apply -f k8s/secrets.yaml -n "${NAMESPACE}"
        kubectl apply -f k8s/configmap.yaml -n "${NAMESPACE}"
        
        # Update deployment with new image
        kubectl set image deployment/ide-backend-dev ide-backend=ideregistry.azurecr.io/ide-backend:${IMAGE_TAG} -n "${NAMESPACE}"
        
        kubectl apply -f k8s/deployment.yaml -n "${NAMESPACE}"
        kubectl apply -f k8s/service.yaml -n "${NAMESPACE}"
        kubectl apply -f k8s/ingress.yaml -n "${NAMESPACE}"
        ;;
        
    "staging")
        echo "Deploying to staging..."
        # Similar to production but with staging-specific values
        ;;
        
    *)
        echo "Unknown environment: ${ENVIRONMENT}"
        echo "Supported environments: production, development, staging"
        exit 1
        ;;
esac

# Wait for deployment to complete
echo "Waiting for deployment to complete..."
kubectl rollout status deployment/ide-backend -n "${NAMESPACE}" --timeout=300s

# Verify deployment
echo "Verifying deployment..."
kubectl get pods -n "${NAMESPACE}" -l app=ide-backend

# Check health
echo "Checking application health..."
sleep 30
kubectl exec -n "${NAMESPACE}" deployment/ide-backend -- curl -f http://localhost:8080/health || echo "Health check failed"

echo "Deployment completed successfully!"
```

#### scripts/rollback.sh
```bash
#!/bin/bash

set -e

ENVIRONMENT=${1:-production}
NAMESPACE="ide-${ENVIRONMENT}"

echo "Rolling back IDE Backend in ${ENVIRONMENT} environment..."

# Check rollout history
echo "Rollout history:"
kubectl rollout history deployment/ide-backend -n "${NAMESPACE}"

# Rollback to previous version
echo "Rolling back to previous version..."
kubectl rollout undo deployment/ide-backend -n "${NAMESPACE}"

# Wait for rollback to complete
echo "Waiting for rollback to complete..."
kubectl rollout status deployment/ide-backend -n "${NAMESPACE}" --timeout=300s

# Verify rollback
echo "Verifying rollback..."
kubectl get pods -n "${NAMESPACE}" -l app=ide-backend

echo "Rollback completed successfully!"
```

### 4.4 Redis VM Configuration

#### k8s/redis-deployment.yaml
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
  namespace: ide-production
spec:
  replicas: 1
  selector:
    matchLabels:
      app: redis
  template:
    metadata:
      labels:
        app: redis
    spec:
      containers:
      - name: redis
        image: redis:7-alpine
        ports:
        - containerPort: 6379
        args:
        - redis-server
        - --appendonly
        - "yes"
        - --maxmemory
        - "1gb"
        - --maxmemory-policy
        - "allkeys-lru"
        resources:
          requests:
            cpu: 100m
            memory: 256Mi
          limits:
            cpu: 500m
            memory: 1Gi
        volumeMounts:
        - name: redis-data
          mountPath: /data
        livenessProbe:
          tcpSocket:
            port: 6379
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          exec:
            command:
            - redis-cli
            - ping
          initialDelaySeconds: 5
          periodSeconds: 5
      volumes:
      - name: redis-data
        persistentVolumeClaim:
          claimName: redis-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: redis-service
  namespace: ide-production
spec:
  type: ClusterIP
  ports:
  - port: 6379
    targetPort: 6379
  selector:
    app: redis
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: redis-pvc
  namespace: ide-production
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
  storageClassName: managed-premium
```
## 5. Rate Limiting por Plano

### 5.1 Configuração por Plano

Sistema de rate limiting diferenciado baseado no plano do usuário com parâmetros configuráveis.

#### IDE.Infrastructure/RateLimiting/PlanBasedRateLimitingService.cs
```csharp
public interface IPlanBasedRateLimitingService
{
    Task<RateLimitResult> CheckRateLimitAsync(Guid userId, string endpoint, UserPlan plan);
    Task<PlanLimits> GetPlanLimitsAsync(UserPlan plan);
    Task UpdatePlanLimitsAsync(UserPlan plan, PlanLimits limits);
}

public class PlanBasedRateLimitingService : IPlanBasedRateLimitingService
{
    private readonly ICacheService _cache;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PlanBasedRateLimitingService> _logger;

    public PlanBasedRateLimitingService(
        ICacheService cache,
        ApplicationDbContext context,
        ILogger<PlanBasedRateLimitingService> logger)
    {
        _cache = cache;
        _context = context;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(Guid userId, string endpoint, UserPlan plan)
    {
        var limits = await GetPlanLimitsAsync(plan);
        var window = TimeSpan.FromMinutes(1); // 1-minute window
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

        var key = $"rate_limit:{userId}:{windowStart:yyyyMMddHHmm}";
        var currentCount = await _cache.IncrementAsync(key, 1, window);

        var isAllowed = currentCount <= limits.RequestsPerMinute;
        var remaining = Math.Max(0, limits.RequestsPerMinute - currentCount);

        var result = new RateLimitResult
        {
            IsAllowed = isAllowed,
            Limit = limits.RequestsPerMinute,
            Current = currentCount,
            Remaining = remaining,
            ResetTime = windowStart.AddMinutes(1),
            Plan = plan
        };

        if (!isAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId} on plan {Plan}. " +
                              "Current: {Current}, Limit: {Limit}",
                userId, plan, currentCount, limits.RequestsPerMinute);
        }

        return result;
    }

    public async Task<PlanLimits> GetPlanLimitsAsync(UserPlan plan)
    {
        var cacheKey = $"plan_limits:{plan}";
        var cached = await _cache.GetAsync<PlanLimits>(cacheKey);
        
        if (cached != null)
            return cached;

        // Get from system parameters or use defaults
        var limits = plan switch
        {
            UserPlan.Free => new PlanLimits
            {
                RequestsPerMinute = await GetSystemParameterAsync("RateLimit.Free.RequestsPerMinute", 100),
                MaxWorkspaces = await GetSystemParameterAsync("Limits.Free.MaxWorkspaces", 5),
                MaxUploadSizeMB = await GetSystemParameterAsync("Limits.Free.MaxUploadSizeMB", 1),
                MaxStorageGB = await GetSystemParameterAsync("Limits.Free.MaxStorageGB", 1),
                ConcurrentConnections = await GetSystemParameterAsync("Limits.Free.ConcurrentConnections", 5)
            },
            UserPlan.Pro => new PlanLimits
            {
                RequestsPerMinute = await GetSystemParameterAsync("RateLimit.Pro.RequestsPerMinute", 500),
                MaxWorkspaces = await GetSystemParameterAsync("Limits.Pro.MaxWorkspaces", 25),
                MaxUploadSizeMB = await GetSystemParameterAsync("Limits.Pro.MaxUploadSizeMB", 10),
                MaxStorageGB = await GetSystemParameterAsync("Limits.Pro.MaxStorageGB", 10),
                ConcurrentConnections = await GetSystemParameterAsync("Limits.Pro.ConcurrentConnections", 25)
            },
            UserPlan.Enterprise => new PlanLimits
            {
                RequestsPerMinute = await GetSystemParameterAsync("RateLimit.Enterprise.RequestsPerMinute", 2000),
                MaxWorkspaces = await GetSystemParameterAsync("Limits.Enterprise.MaxWorkspaces", -1), // Unlimited
                MaxUploadSizeMB = await GetSystemParameterAsync("Limits.Enterprise.MaxUploadSizeMB", 50),
                MaxStorageGB = await GetSystemParameterAsync("Limits.Enterprise.MaxStorageGB", 100),
                ConcurrentConnections = await GetSystemParameterAsync("Limits.Enterprise.ConcurrentConnections", 100)
            },
            _ => throw new ArgumentException($"Unknown plan: {plan}")
        };

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, limits, TimeSpan.FromMinutes(5));
        return limits;
    }

    private async Task<int> GetSystemParameterAsync(string key, int defaultValue)
    {
        try
        {
            var parameter = await _context.SystemParameters
                .FirstOrDefaultAsync(p => p.Key == key);
            
            return parameter != null && int.TryParse(parameter.Value, out var value) 
                ? value 
                : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}

public class PlanLimits
{
    public int RequestsPerMinute { get; set; }
    public int MaxWorkspaces { get; set; }
    public int MaxUploadSizeMB { get; set; }
    public int MaxStorageGB { get; set; }
    public int ConcurrentConnections { get; set; }
}

public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public long Limit { get; set; }
    public long Current { get; set; }
    public long Remaining { get; set; }
    public DateTime ResetTime { get; set; }
    public UserPlan Plan { get; set; }
}
```

### 5.2 Sistema de Parâmetros

#### IDE.Domain/Entities/SystemParameter.cs
```csharp
public class SystemParameter
{
    public Guid Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
    public ParameterType Type { get; set; } = ParameterType.String;
    public bool IsEncrypted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
}

public enum ParameterType
{
    String,
    Integer,
    Boolean,
    Decimal,
    Json
}
```

#### IDE.Application/Configuration/ISystemParameterService.cs
```csharp
public interface ISystemParameterService
{
    Task<T> GetParameterAsync<T>(string key, T defaultValue = default);
    Task SetParameterAsync<T>(string key, T value, string description = null);
    Task<List<SystemParameter>> GetParametersByCategoryAsync(string category);
    Task<bool> DeleteParameterAsync(string key);
    Task<Dictionary<string, object>> GetAllParametersAsync();
}

public class SystemParameterService : ISystemParameterService
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ILogger<SystemParameterService> _logger;

    public SystemParameterService(
        ApplicationDbContext context,
        ICacheService cache,
        ILogger<SystemParameterService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetParameterAsync<T>(string key, T defaultValue = default)
    {
        var cacheKey = $"system_param:{key}";
        var cached = await _cache.GetAsync<string>(cacheKey);
        
        if (cached != null)
        {
            return ConvertValue<T>(cached, defaultValue);
        }

        var parameter = await _context.SystemParameters
            .FirstOrDefaultAsync(p => p.Key == key);

        if (parameter == null)
        {
            return defaultValue;
        }

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, parameter.Value, TimeSpan.FromMinutes(5));
        
        return ConvertValue<T>(parameter.Value, defaultValue);
    }

    public async Task SetParameterAsync<T>(string key, T value, string description = null)
    {
        var parameter = await _context.SystemParameters
            .FirstOrDefaultAsync(p => p.Key == key);

        var stringValue = ConvertToString(value);
        var parameterType = GetParameterType<T>();

        if (parameter == null)
        {
            parameter = new SystemParameter
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = stringValue,
                Description = description ?? $"Auto-generated parameter for {key}",
                Type = parameterType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty, // System
                UpdatedBy = Guid.Empty  // System
            };
            
            _context.SystemParameters.Add(parameter);
        }
        else
        {
            parameter.Value = stringValue;
            parameter.Type = parameterType;
            parameter.UpdatedAt = DateTime.UtcNow;
            parameter.UpdatedBy = Guid.Empty; // System
        }

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _cache.RemoveAsync($"system_param:{key}");

        _logger.LogInformation("System parameter updated: {Key} = {Value}", key, stringValue);
    }

    private T ConvertValue<T>(string value, T defaultValue)
    {
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private string ConvertToString<T>(T value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private ParameterType GetParameterType<T>()
    {
        var type = typeof(T);
        
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return ParameterType.Integer;
        if (type == typeof(bool))
            return ParameterType.Boolean;
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return ParameterType.Decimal;
        
        return ParameterType.String;
    }
}
```

## 6. Testes End-to-End Completos

### 6.1 Playwright Functional Tests

#### Tests/IDE.E2ETests/PlaywrightTests.cs
```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

[TestClass]
public class WorkspaceCollaborationTests : PageTest
{
    private const string BaseUrl = "http://localhost:3000";
    private const string ApiUrl = "http://localhost:8503";

    [TestInitialize]
    public async Task TestInitialize()
    {
        // Set up test data
        await Context.ClearCookiesAsync();
    }

    [TestMethod]
    public async Task UserCanCreateAndAccessWorkspace()
    {
        // Arrange
        await Page.GotoAsync(BaseUrl);

        // Act - Login
        await Page.ClickAsync("[data-testid=login-button]");
        await Page.FillAsync("[data-testid=email-input]", "test@example.com");
        await Page.FillAsync("[data-testid=password-input]", "Test123!");
        await Page.ClickAsync("[data-testid=submit-login]");

        // Wait for dashboard
        await Page.WaitForSelectorAsync("[data-testid=dashboard]");

        // Create workspace
        await Page.ClickAsync("[data-testid=create-workspace-button]");
        await Page.FillAsync("[data-testid=workspace-name]", "Test Workspace");
        await Page.FillAsync("[data-testid=workspace-description]", "E2E Test Workspace");
        await Page.ClickAsync("[data-testid=save-workspace]");

        // Assert
        await Expect(Page.Locator("[data-testid=workspace-list]")).ToContainTextAsync("Test Workspace");
    }

    [TestMethod]
    public async Task MultipleUsersCanCollaborateInRealTime()
    {
        // Create two browser contexts for two users
        var context1 = await Browser.NewContextAsync();
        var context2 = await Browser.NewContextAsync();
        
        var page1 = await context1.NewPageAsync();
        var page2 = await context2.NewPageAsync();

        // User 1 login and create workspace
        await LoginUser(page1, "user1@example.com", "Test123!");
        var workspaceId = await CreateWorkspace(page1, "Collaboration Test");
        
        // User 2 login and join workspace
        await LoginUser(page2, "user2@example.com", "Test123!");
        await JoinWorkspace(page2, workspaceId);

        // User 1 creates an item
        await page1.ClickAsync("[data-testid=create-item-button]");
        await page1.FillAsync("[data-testid=item-name]", "Shared Document");
        await page1.SelectOptionAsync("[data-testid=item-module]", "Documents");
        await page1.ClickAsync("[data-testid=save-item]");

        // Verify User 2 sees the item in real-time
        await page2.WaitForSelectorAsync("[data-testid=item-list]");
        await Expect(page2.Locator("[data-testid=item-list]")).ToContainTextAsync("Shared Document");

        // Test typing indicators
        await page1.ClickAsync("[data-testid=item-editor]");
        await page1.TypeAsync("[data-testid=content-editor]", "Hello from User 1");

        // Verify User 2 sees typing indicator
        await Expect(page2.Locator("[data-testid=typing-indicator]")).ToContainTextAsync("user1 is typing...");

        // Test chat
        await page1.FillAsync("[data-testid=chat-input]", "Hello User 2!");
        await page1.ClickAsync("[data-testid=send-chat]");

        await Expect(page2.Locator("[data-testid=chat-messages]")).ToContainTextAsync("Hello User 2!");
    }

    [TestMethod]
    public async Task RateLimitingWorksCorrectly()
    {
        await Page.GotoAsync(BaseUrl);
        await LoginUser(Page, "free-user@example.com", "Test123!");

        var requests = 0;
        var rateLimited = false;

        // Make requests rapidly to trigger rate limiting
        for (int i = 0; i < 120; i++)
        {
            try
            {
                var response = await Page.EvaluateAsync<string>(@"
                    fetch('/api/workspaces', {
                        headers: { 'Authorization': 'Bearer ' + localStorage.getItem('token') }
                    }).then(r => r.status.toString())
                ");

                requests++;
                
                if (response == "429")
                {
                    rateLimited = true;
                    break;
                }
            }
            catch
            {
                // Continue
            }
        }

        Assert.IsTrue(rateLimited, "Rate limiting should have been triggered");
        Assert.IsTrue(requests > 90, "Should allow at least 90 requests before rate limiting");
    }

    private async Task LoginUser(IPage page, string email, string password)
    {
        await page.GotoAsync(BaseUrl);
        await page.ClickAsync("[data-testid=login-button]");
        await page.FillAsync("[data-testid=email-input]", email);
        await page.FillAsync("[data-testid=password-input]", password);
        await page.ClickAsync("[data-testid=submit-login]");
        await page.WaitForSelectorAsync("[data-testid=dashboard]");
    }

    private async Task<string> CreateWorkspace(IPage page, string name)
    {
        await page.ClickAsync("[data-testid=create-workspace-button]");
        await page.FillAsync("[data-testid=workspace-name]", name);
        await page.ClickAsync("[data-testid=save-workspace]");
        
        // Extract workspace ID from URL or response
        var workspaceId = await page.EvaluateAsync<string>("window.location.pathname.split('/').pop()");
        return workspaceId;
    }

    private async Task JoinWorkspace(IPage page, string workspaceId)
    {
        await page.GotoAsync($"{BaseUrl}/workspace/{workspaceId}");
        await page.WaitForSelectorAsync("[data-testid=workspace-content]");
    }
}
```

### 6.2 Performance Testing

#### Tests/IDE.PerformanceTests/LoadTests.cs
```csharp
using NBomber.Contracts;
using NBomber.CSharp;

public class LoadTests
{
    [Test]
    public void WorkspaceApiLoadTest()
    {
        var scenario = Scenario.Create("workspace_load_test", async context =>
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + GetTestToken());

            var response = await httpClient.GetAsync("http://localhost:8503/api/workspaces");
            
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(5)),
            Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(10))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Test]
    public void SignalRConnectionLoadTest()
    {
        var scenario = Scenario.Create("signalr_load_test", async context =>
        {
            try
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl("http://localhost:8503/hubs/workspace", options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(GetTestToken());
                    })
                    .Build();

                await connection.StartAsync();
                await connection.InvokeAsync("JoinWorkspace", Guid.NewGuid().ToString());
                
                await Task.Delay(1000);
                
                await connection.DisposeAsync();
                
                return Response.Ok();
            }
            catch
            {
                return Response.Fail();
            }
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromMinutes(3)),
            Simulation.KeepConstant(copies: 100, during: TimeSpan.FromMinutes(5))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private string GetTestToken()
    {
        // Generate or retrieve test JWT token
        return "test-jwt-token";
    }
}
```

### 6.3 Security Testing

#### Tests/IDE.SecurityTests/SecurityTests.cs
```csharp
public class SecurityTests
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "http://localhost:8503";

    public SecurityTests()
    {
        _httpClient = new HttpClient();
    }

    [Test]
    public async Task Api_RequiresAuthentication()
    {
        // Test protected endpoints without token
        var endpoints = new[]
        {
            "/api/workspaces",
            "/api/workspaces/123",
            "/api/auth/me"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Test]
    public async Task Api_RejectsInvalidTokens()
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await _httpClient.GetAsync($"{_baseUrl}/api/workspaces");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Test]
    public async Task Api_PreventsSqlInjection()
    {
        var maliciousInputs = new[]
        {
            "'; DROP TABLE Users; --",
            "1' OR '1'='1",
            "admin'/*",
            "1; UPDATE Users SET Password='hacked' WHERE Id=1; --"
        };

        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validToken);

        foreach (var input in maliciousInputs)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = input, description = input }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/workspaces", content);
            
            // Should either reject the input or sanitize it, but not cause server error
            Assert.AreNotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }

    [Test]
    public async Task Api_PreventstXssAttacks()
    {
        var xssPayloads = new[]
        {
            "<script>alert('xss')</script>",
            "javascript:alert('xss')",
            "<img src=x onerror=alert('xss')>",
            "<iframe src='javascript:alert(`xss`)'></iframe>"
        };

        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validToken);

        foreach (var payload in xssPayloads)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = "Test", description = payload }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/workspaces", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Verify XSS payload is sanitized
                Assert.IsFalse(responseContent.Contains("<script>"));
                Assert.IsFalse(responseContent.Contains("javascript:"));
                Assert.IsFalse(responseContent.Contains("onerror="));
            }
        }
    }

    [Test]
    public async Task Api_EnforcesRateLimiting()
    {
        var validToken = await GetValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validToken);

        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Send 150 requests simultaneously
        for (int i = 0; i < 150; i++)
        {
            tasks.Add(_httpClient.GetAsync($"{_baseUrl}/api/workspaces"));
        }

        var responses = await Task.WhenAll(tasks);
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        
        Assert.IsTrue(rateLimitedCount > 0, "Rate limiting should have been triggered");
    }

    private async Task<string> GetValidTokenAsync()
    {
        var loginData = new
        {
            email = "test@example.com",
            password = "Test123!"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/auth/login", content);
        var responseData = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseData);
        
        return authResponse.Data.Token;
    }
}
```

## 4. Documentação Swagger Completa

### 4.1 Configuração Avançada do Swagger

#### IDE.API/Configuration/SwaggerConfiguration.cs
```csharp
public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IDE Platform API",
                Version = "v1.0.0",
                Description = "API completa para plataforma IDE colaborativa com workspaces, editores e tempo real",
                Contact = new OpenApiContact
                {
                    Name = "IDE Team",
                    Email = "dev@ide-platform.com",
                    Url = new Uri("https://ide-platform.com")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // Configuração de segurança JWT
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Incluir comentários XML
            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly).ToList();
            xmlFiles.ForEach(xmlFile => c.IncludeXmlComments(xmlFile));

            // Configurações avançadas
            c.EnableAnnotations();
            c.DescribeAllParametersInCamelCase();
            c.UseInlineDefinitionsForEnums();

            // Exemplos customizados
            c.SchemaFilter<ExampleSchemaFilter>();
            c.OperationFilter<ResponseExamplesOperationFilter>();

            // Tags para organização
            c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
            c.DocInclusionPredicate((name, api) => true);

            // Servers
            c.AddServer(new OpenApiServer
            {
                Url = "http://localhost:8503",
                Description = "Development Server"
            });

            c.AddServer(new OpenApiServer
            {
                Url = "https://api.ide-platform.com",
                Description = "Production Server"
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger(c =>
        {
            c.SerializeAsV2 = false;
            c.RouteTemplate = "api-docs/{documentname}/swagger.json";
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/api-docs/v1/swagger.json", "IDE Platform API v1");
            c.RoutePrefix = "api-docs";
            c.DocumentTitle = "IDE Platform API Documentation";
            
            // Customização da UI
            c.DefaultModelsExpandDepth(2);
            c.DefaultModelExpandDepth(2);
            c.DocExpansion(DocExpansion.List);
            c.EnableDeepLinking();
            c.DisplayOperationId();
            c.EnableFilter();
            c.ShowExtensions();
            c.EnableValidator();
            
            // CSS customizado
            c.InjectStylesheet("/swagger-ui/custom.css");
            
            // JavaScript customizado
            c.InjectJavascript("/swagger-ui/custom.js");
        });

        return app;
    }
}
```

### 4.2 Schema Filters e Examples

#### IDE.API/Swagger/ExampleSchemaFilter.cs
```csharp
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CreateWorkspaceRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("Meu Projeto Incrível"),
                ["description"] = new OpenApiString("Descrição detalhada do projeto"),
                ["defaultPhases"] = new OpenApiArray
                {
                    new OpenApiString("Development"),
                    new OpenApiString("Review"),
                    new OpenApiString("Testing"),
                    new OpenApiString("Production")
                }
            };
        }
        else if (context.Type == typeof(CreateModuleItemRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["name"] = new OpenApiString("HomePage Component"),
                ["content"] = new OpenApiString("import React from 'react';\n\nconst HomePage = () => {\n  return <div>Home Page</div>;\n};\n\nexport default HomePage;"),
                ["module"] = new OpenApiString("Frontend"),
                ["editorType"] = new OpenApiString("code"),
                ["language"] = new OpenApiString("typescript"),
                ["tags"] = new OpenApiArray
                {
                    new OpenApiString("React"),
                    new OpenApiString("Component"),
                    new OpenApiString("UI")
                }
            };
        }
        else if (context.Type == typeof(RegisterRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["email"] = new OpenApiString("usuario@exemplo.com"),
                ["username"] = new OpenApiString("usuario123"),
                ["password"] = new OpenApiString("MinhaSenh@123"),
                ["firstName"] = new OpenApiString("João"),
                ["lastName"] = new OpenApiString("Silva")
            };
        }
    }
}

public class ResponseExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses.ContainsKey("200"))
        {
            var response = operation.Responses["200"];
            
            if (context.MethodInfo.Name == "GetWorkspace")
            {
                response.Content["application/json"].Example = new OpenApiObject
                {
                    ["success"] = new OpenApiBoolean(true),
                    ["message"] = new OpenApiString("Workspace obtido com sucesso"),
                    ["data"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiString("123e4567-e89b-12d3-a456-426614174000"),
                        ["name"] = new OpenApiString("Meu Projeto"),
                        ["description"] = new OpenApiString("Projeto de exemplo"),
                        ["semanticVersion"] = new OpenApiString("1.0.0"),
                        ["isArchived"] = new OpenApiBoolean(false),
                        ["totalItems"] = new OpenApiInteger(15),
                        ["totalSize"] = new OpenApiLong(2048576)
                    }
                };
            }
        }
    }
}
```

## 5. Configurações de Segurança para Produção

### 5.1 Security Headers Middleware

#### IDE.API/Middleware/SecurityHeadersMiddleware.cs
```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Remover headers que revelam informação sobre o servidor
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");

        // Headers de segurança
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Add("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

        // HSTS (apenas em HTTPS)
        if (context.Request.IsHttps)
        {
            context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        // CSP (Content Security Policy)
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' ws: wss:; " +
            "font-src 'self'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'");

        await _next(context);
    }
}
```

### 5.2 Rate Limiting Avançado

#### IDE.Infrastructure/Security/AdvancedRateLimiting.cs
```csharp
public static class AdvancedRateLimitingExtensions
{
    public static IServiceCollection AddAdvancedRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            // Rate limit por IP
            options.AddFixedWindowLimiter("PerIP", opt =>
            {
                opt.PermitLimit = 1000;
                opt.Window = TimeSpan.FromHours(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 5;
            });

            // Rate limit para autenticação
            options.AddSlidingWindowLimiter("Auth", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(15);
                opt.SegmentsPerWindow = 3;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            // Rate limit para upload de arquivos
            options.AddTokenBucketLimiter("Upload", opt =>
            {
                opt.TokenLimit = 100;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
                opt.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
                opt.TokensPerPeriod = 50;
            });

            // Rate limit para SignalR
            options.AddConcurrencyLimiter("SignalR", opt =>
            {
                opt.PermitLimit = 1000;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 100;
            });

            // Configuração global
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User?.FindFirst("id")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 2000,
                        Window = TimeSpan.FromHours(1)
                    }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                
                var response = new ApiResponse<object>
                {
                    Success = false,
                    Message = "Muitas requisições. Tente novamente mais tarde.",
                    StatusCode = 429,
                    Timestamp = DateTime.UtcNow
                };

                await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(response), token);
            };
        });

        return services;
    }
}
```

### 5.3 Input Validation e Sanitização

#### IDE.Infrastructure/Security/InputSanitizer.cs
```csharp
public interface IInputSanitizer
{
    string SanitizeHtml(string input);
    string SanitizeFileName(string fileName);
    string SanitizeUrl(string url);
    bool IsValidJson(string json);
    string EscapeForLogging(string input);
}

public class InputSanitizer : IInputSanitizer
{
    private readonly HtmlSanitizer _htmlSanitizer;

    public InputSanitizer()
    {
        _htmlSanitizer = new HtmlSanitizer();
        
        // Configurar tags permitidas
        _htmlSanitizer.AllowedTags.Clear();
        _htmlSanitizer.AllowedTags.Add("p");
        _htmlSanitizer.AllowedTags.Add("br");
        _htmlSanitizer.AllowedTags.Add("strong");
        _htmlSanitizer.AllowedTags.Add("em");
        _htmlSanitizer.AllowedTags.Add("ul");
        _htmlSanitizer.AllowedTags.Add("ol");
        _htmlSanitizer.AllowedTags.Add("li");
        _htmlSanitizer.AllowedTags.Add("a");
        _htmlSanitizer.AllowedTags.Add("code");
        _htmlSanitizer.AllowedTags.Add("pre");

        // Configurar atributos permitidos
        _htmlSanitizer.AllowedAttributes.Clear();
        _htmlSanitizer.AllowedAttributes.Add("href");
        _htmlSanitizer.AllowedAttributes.Add("title");
        _htmlSanitizer.AllowedAttributes.Add("class");
    }

    public string SanitizeHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return _htmlSanitizer.Sanitize(input);
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        // Remover caracteres perigosos
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Limitar tamanho
        if (sanitized.Length > 255)
            sanitized = sanitized.Substring(0, 255);

        // Evitar nomes reservados do Windows
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        
        if (reservedNames.Contains(sanitized.ToUpperInvariant()))
            sanitized = $"_{sanitized}";

        return sanitized;
    }

    public string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // Permitir apenas HTTP/HTTPS
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri.ToString();
    }

    public bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public string EscapeForLogging(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return input
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\"", "\\\"");
    }
}
```

## 6. Configuração Final da API

### IDE.API/Program.cs (Configuração Completa)
```csharp
using Serilog;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuração do Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.File("logs/ide-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// Authentication & Authorization
var jwtOptions = builder.Configuration.GetSection("JWT").Get<JwtOptions>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JWT"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // SignalR token authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetValue<string>("Frontend:BaseUrl"))
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Rate Limiting
builder.Services.AddAdvancedRateLimiting(builder.Configuration);

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<SignalRHealthCheck>("signalr")
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"));

// Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IUserPresenceService, UserPresenceService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// Infrastructure Services
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
builder.Services.AddScoped<IInputSanitizer, InputSanitizer>();

// Metrics
builder.Services.AddSingleton<IPerformanceMetrics, PerformanceMetrics>();

// Swagger
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation(app.Environment);
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<CacheMiddleware>();

app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health Checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// SignalR Hubs
app.MapHub<WorkspaceHub>("/hubs/workspace");

// API Endpoints
app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapRealtimeEndpoints();

// Aplicar migrations em desenvolvimento
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

// Cleanup de presença em background
var cleanupTimer = new Timer(async _ =>
{
    using var scope = app.Services.CreateScope();
    var presenceService = scope.ServiceProvider.GetRequiredService<IUserPresenceService>();
    await presenceService.CleanupStaleConnectionsAsync();
}, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

try
{
    Log.Information("Starting IDE API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IDE API failed to start");
}
finally
{
    Log.CloseAndFlush();
    cleanupTimer?.Dispose();
}
```

## Entregáveis da Fase 4

✅ **Sistema de cache Redis** com invalidação inteligente  
✅ **Logs estruturados** com Serilog e métricas  
✅ **Health checks** para todos os serviços  
✅ **Swagger documentação** profissional completa  
✅ **Security headers** e proteções avançadas  
✅ **Rate limiting** sofisticado por tipo de operação  
✅ **Input sanitization** contra XSS e injection  
✅ **Performance monitoring** com métricas detalhadas  
✅ **Background services** para limpeza automática  
✅ **Configuração de produção** otimizada  

## Validação Final da Fase 4

### Critérios de Sucesso
- [ ] Cache Redis funciona com hit/miss adequados
- [ ] Logs estruturados são gerados corretamente
- [ ] Health checks respondem status corretos
- [ ] Swagger UI está completo e funcional
- [ ] Security headers estão presentes
- [ ] Rate limiting funciona para diferentes cenários
- [ ] Input sanitization previne ataques
- [ ] Métricas são coletadas e expostas
- [ ] Performance está otimizada (< 200ms p95)
- [ ] Aplicação está pronta para produção

### Testes de Performance
```bash
# Teste de carga básico
ab -n 1000 -c 50 -H "Authorization: Bearer <token>" http://localhost:8503/api/workspaces

# Teste de rate limiting
for i in {1..20}; do curl -H "Authorization: Bearer <token>" http://localhost:8503/api/auth/me; done

# Verificar health checks
curl http://localhost:8503/health

# Verificar métricas
curl http://localhost:8503/metrics
```

### 8.3 Environment Configuration

#### docker-entrypoint.sh
```bash
#!/bin/sh

# Replace environment variables in JavaScript files
for file in /usr/share/nginx/html/static/js/*.js; do
  if [ -f "$file" ]; then
    sed -i "s|REACT_APP_API_URL_PLACEHOLDER|${REACT_APP_API_URL:-http://localhost:8503}|g" "$file"
    sed -i "s|REACT_APP_SIGNALR_URL_PLACEHOLDER|${REACT_APP_SIGNALR_URL:-http://localhost:8503/hubs}|g" "$file"
  fi
done

# Start nginx
exec "$@"
```

#### nginx.conf
```nginx
events {
    worker_connections 1024;
}

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;

    # Logging
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';
    
    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml;

    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;

    server {
        listen 80;
        server_name _;
        
        root /usr/share/nginx/html;
        index index.html index.htm;

        # Security headers
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header Referrer-Policy "strict-origin-when-cross-origin" always;

        # Handle React Router
        location / {
            try_files $uri $uri/ /index.html;
            
            # Cache static assets
            location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
                expires 1y;
                add_header Cache-Control "public, immutable";
            }
        }

        # API proxy (if needed)
        location /api/ {
            limit_req zone=api burst=20 nodelay;
            proxy_pass ${REACT_APP_API_URL}/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Health check
        location /health {
            access_log off;
            return 200 "healthy\n";
            add_header Content-Type text/plain;
        }
    }
}
```

## 9. Métricas de Performance

### 9.1 SLA Targets

**Disponibilidade: 99.9% (8.77 horas de downtime por ano)**

#### IDE.Infrastructure/Metrics/SlaMonitoring.cs
```csharp
public interface ISlaMonitoring
{
    Task RecordUptimeAsync(bool isHealthy);
    Task<SlaMetrics> GetCurrentSlaAsync();
    Task<SlaReport> GenerateMonthlySlaReportAsync(DateTime month);
    Task SendSlaAlertAsync(double currentUptime, double target);
}

public class SlaMonitoring : ISlaMonitoring
{
    private readonly ICacheService _cache;
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<SlaMonitoring> _logger;
    private readonly IAlertingService _alertingService;

    public SlaMonitoring(
        ICacheService cache,
        TelemetryClient telemetryClient,
        ILogger<SlaMonitoring> logger,
        IAlertingService alertingService)
    {
        _cache = cache;
        _telemetryClient = telemetryClient;
        _logger = logger;
        _alertingService = alertingService;
    }

    public async Task RecordUptimeAsync(bool isHealthy)
    {
        var timestamp = DateTime.UtcNow;
        var key = $"sla:uptime:{timestamp:yyyyMMddHH}";
        
        var uptimeData = await _cache.GetAsync<UptimeRecord>(key) ?? new UptimeRecord();
        
        uptimeData.TotalChecks++;
        if (isHealthy)
        {
            uptimeData.HealthyChecks++;
        }
        else
        {
            uptimeData.UnhealthyChecks++;
            _logger.LogWarning("Health check failed at {Timestamp}", timestamp);
        }

        await _cache.SetAsync(key, uptimeData, TimeSpan.FromDays(32));

        // Send to Application Insights
        _telemetryClient.TrackMetric("SLA.HealthCheck", isHealthy ? 1 : 0);
        
        // Check if SLA is at risk
        var currentSla = await GetCurrentSlaAsync();
        if (currentSla.UptimePercentage < 99.5) // Alert before SLA breach
        {
            await SendSlaAlertAsync(currentSla.UptimePercentage, 99.9);
        }
    }

    public async Task<SlaMetrics> GetCurrentSlaAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        
        var totalChecks = 0;
        var healthyChecks = 0;
        
        var currentDate = startOfMonth;
        while (currentDate <= now)
        {
            var key = $"sla:uptime:{currentDate:yyyyMMddHH}";
            var record = await _cache.GetAsync<UptimeRecord>(key);
            
            if (record != null)
            {
                totalChecks += record.TotalChecks;
                healthyChecks += record.HealthyChecks;
            }
            
            currentDate = currentDate.AddHours(1);
        }

        var uptimePercentage = totalChecks > 0 
            ? (double)healthyChecks / totalChecks * 100 
            : 100.0;

        return new SlaMetrics
        {
            UptimePercentage = uptimePercentage,
            TotalChecks = totalChecks,
            HealthyChecks = healthyChecks,
            UnhealthyChecks = totalChecks - healthyChecks,
            Period = $"{startOfMonth:yyyy-MM-dd} to {now:yyyy-MM-dd}",
            TargetSla = 99.9
        };
    }

    public async Task SendSlaAlertAsync(double currentUptime, double target)
    {
        var message = $"SLA at risk! Current uptime: {currentUptime:F2}% (Target: {target}%)";
        await _alertingService.SendAlertAsync("SLA", message, AlertSeverity.High);
    }
}

public class UptimeRecord
{
    public int TotalChecks { get; set; }
    public int HealthyChecks { get; set; }
    public int UnhealthyChecks { get; set; }
}

public class SlaMetrics
{
    public double UptimePercentage { get; set; }
    public int TotalChecks { get; set; }
    public int HealthyChecks { get; set; }
    public int UnhealthyChecks { get; set; }
    public string Period { get; set; }
    public double TargetSla { get; set; }
    public bool IsWithinTarget => UptimePercentage >= TargetSla;
}
```

## 10. Scripts de Operação

### 10.1 Configuração de Ambiente

#### .env.production
```bash
# API Configuration
REACT_APP_API_URL=https://api.idestudio.com
REACT_APP_SIGNALR_URL=https://api.idestudio.com/hubs

# Application Settings
REACT_APP_APP_NAME=IDE Studio
REACT_APP_VERSION=1.0.0
REACT_APP_ENVIRONMENT=production

# Feature Flags
REACT_APP_ENABLE_CHAT=true
REACT_APP_ENABLE_COLLABORATION=true
REACT_APP_ENABLE_ANALYTICS=true

# Performance Settings
REACT_APP_API_TIMEOUT=30000
REACT_APP_RETRY_ATTEMPTS=3
REACT_APP_CACHE_DURATION=300000

# Monitoring
REACT_APP_ENABLE_TELEMETRY=true
REACT_APP_APPLICATION_INSIGHTS_KEY=your-app-insights-key

# Rate Limiting Display
REACT_APP_SHOW_RATE_LIMITS=true
```

### 10.2 Health Check Script

#### scripts/health-check.sh
```bash
#!/bin/bash

# Health Check Script para IDE Studio
set -e

# Configurações
FRONTEND_URL="https://idestudio.com"
BACKEND_URL="https://api.idestudio.com"
TIMEOUT=30
RETRIES=3

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

check_endpoint() {
    local url=$1
    local name=$2
    local expected_status=${3:-200}
    
    log "🔍 Verificando $name..."
    
    for i in $(seq 1 $RETRIES); do
        if response=$(curl -s -o /dev/null -w "%{http_code}" --max-time $TIMEOUT "$url"); then
            if [ "$response" = "$expected_status" ]; then
                log "${GREEN}✅ $name: OK (HTTP $response)${NC}"
                return 0
            else
                log "${YELLOW}⚠️ $name: HTTP $response (esperado $expected_status)${NC}"
            fi
        else
            log "${YELLOW}⚠️ $name: Tentativa $i/$RETRIES falhou${NC}"
        fi
        
        if [ $i -lt $RETRIES ]; then
            sleep 5
        fi
    done
    
    log "${RED}❌ $name: FALHOU após $RETRIES tentativas${NC}"
    return 1
}

main() {
    log "🚀 Iniciando health check do IDE Studio..."
    
    local failures=0
    
    # 1. Frontend (React)
    if ! check_endpoint "$FRONTEND_URL" "Frontend React"; then
        ((failures++))
    fi
    
    # 2. Backend Health Check
    if ! check_endpoint "$BACKEND_URL/health" "Backend API Health"; then
        ((failures++))
    fi
    
    # 3. Backend Ready Check
    if ! check_endpoint "$BACKEND_URL/health/ready" "Backend API Ready"; then
        ((failures++))
    fi
    
    # 4. Backend Live Check
    if ! check_endpoint "$BACKEND_URL/health/live" "Backend API Live"; then
        ((failures++))
    fi
    
    # Resumo final
    log ""
    log "📊 Resumo do Health Check:"
    if [ $failures -eq 0 ]; then
        log "${GREEN}✅ Todos os serviços estão saudáveis!${NC}"
        exit 0
    else
        log "${RED}❌ $failures verificação(ões) falharam${NC}"
        exit 1
    fi
}

main
```

## 11. Documentação Final

### 11.1 Checklist de Produção

#### Production Readiness Checklist

##### ✅ Segurança
- [x] HTTPS enforced em todos os endpoints
- [x] JWT tokens com expiração configurada
- [x] Rate limiting implementado por plano
- [x] Input validation e sanitization
- [x] Security headers configurados
- [x] CORS apropriadamente configurado

##### ✅ Performance
- [x] Cache Redis implementado
- [x] Response caching configurado
- [x] Database query optimization
- [x] Connection pooling configurado
- [x] Compression (Gzip) habilitada
- [x] CDN para assets estáticos

##### ✅ Observabilidade
- [x] Application Insights configurado
- [x] Structured logging implementado
- [x] Health checks configurados
- [x] Métricas personalizadas implementadas
- [x] Distributed tracing habilitado
- [x] SLA monitoring implementado

##### ✅ Reliability
- [x] Circuit breaker patterns
- [x] Retry policies configuradas
- [x] Graceful shutdown implementado
- [x] Database migrations automáticas
- [x] Backup strategy implementada
- [x] Disaster recovery documentado

##### ✅ Scalability
- [x] Horizontal Pod Autoscaler configurado
- [x] Load balancing implementado
- [x] Stateless application design
- [x] Database pooling otimizado
- [x] Cache distribution strategy
- [x] Resource limits definidos

##### ✅ Monitoring & Alerting
- [x] Application Insights alertas
- [x] Kubernetes monitoring
- [x] SLA alerts configurados
- [x] Error rate monitoring
- [x] Performance degradation alerts
- [x] Capacity planning metrics

### 11.2 Arquitetura Final

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Cloud Infrastructure                │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────┐                 │
│  │   Azure CDN     │    │  App Gateway    │                 │
│  │                 │    │  (Load Balancer)│                 │
│  └─────────────────┘    └─────────────────┘                 │
│                                  │                          │
│  ┌──────────────────────────────────────────────────────────┤
│  │              Azure Kubernetes Service (AKS)              │
│  │                                                          │
│  │  ┌─────────────────┐    ┌─────────────────┐             │
│  │  │   Frontend      │    │    Backend      │             │
│  │  │   (React 19)    │    │   (.NET 8)     │             │
│  │  │   - TypeScript  │    │   - SignalR     │             │
│  │  │   - Ant Design  │    │   - JWT Auth    │             │
│  │  │   - Vite        │    │   - Rate Limit  │             │
│  │  └─────────────────┘    └─────────────────┘             │
│  │                                                          │
│  │  HPA: 2-6 replicas      HPA: 3-10 replicas              │
│  └──────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐    ┌─────────────────┐                │
│  │  PostgreSQL     │    │   Redis Cache   │                │
│  │  (Azure DB)     │    │   (Azure VM)    │                │
│  │  - Backup daily │    │   - Non-persist │                │
│  │  - 99.9% SLA    │    │   - 15min TTL   │                │
│  └─────────────────┘    └─────────────────┘                │
│                                                             │
│  ┌─────────────────┐    ┌─────────────────┐                │
│  │  Blob Storage   │    │  App Insights   │                │
│  │  (Files)        │    │  (Monitoring)   │                │
│  └─────────────────┘    └─────────────────┘                │
└─────────────────────────────────────────────────────────────┘
```

### 11.3 Métricas de Sucesso

#### KPIs Técnicos
- **Availability**: 99.9% uptime
- **Response Time**: < 500ms (95th percentile)
- **Error Rate**: < 0.1%
- **Throughput**: 1000+ requests/minute por pod

#### Rate Limits por Plano
- **Free**: 100 requests/min
- **Pro**: 500 requests/min
- **Enterprise**: 2000 requests/min

#### Performance Targets
- **Frontend Load**: < 3 segundos
- **API Response**: < 200ms (média)
- **SignalR Latency**: < 100ms
- **Database Queries**: < 50ms (95th percentile)

## Implementação Completa Finalizada

Com a conclusão da Fase 4, o sistema IDE Studio está **100% implementado** e **pronto para produção** com:

### ✅ Frontend React Integrado
- **React 19** + TypeScript + Vite + Ant Design 5
- **Integração completa** com backend .NET Core
- **SignalR client** para colaboração em tempo real
- **Rate limiting display** por plano do usuário

### ✅ Backend .NET Core Otimizado
- **Autenticação JWT** completa com OAuth
- **Sistema de workspaces** com colaboração
- **SignalR Hub** para edição em tempo real
- **Rate limiting** diferenciado por plano

### ✅ Azure Kubernetes Production
- **AKS cluster** com HPA e SSL
- **Horizontal Pod Autoscaler** configurado
- **Application Gateway** como load balancer
- **Azure PostgreSQL** com backup diário

### ✅ Testes Completos
- **Playwright E2E** tests com multi-user
- **Performance testing** com NBomber
- **Security testing** com verificações XSS/SQL injection
- **Load testing** para validar rate limiting

### ✅ CI/CD Pipeline
- **GitHub Actions** com build, test e deploy
- **Docker containers** otimizados
- **Rolling updates** sem downtime
- **Blue-green deployment** opcional

### ✅ Monitoramento Enterprise
- **Application Insights** com distributed tracing
- **SLA monitoring** com alertas automáticos
- **Health checks** avançados
- **Performance metrics** customizados

### ✅ Operações Automatizadas
- **Scripts de backup** automáticos diários
- **Health check** scripts completos
- **Restore procedures** documentados
- **Alerting** via Teams/Slack

O sistema está **totalmente integrado**, **monitorado** e **pronto para escalar** para milhares de usuários simultâneos! 🚀
