# Fase 3 (Parte 9) – Endpoints REST & Configuração da Aplicação

> Complemento ao canal em tempo real (SignalR). Fornece acesso REST para histórico, sincronização, métricas e fallback.

## 1. Objetivo
Oferecer operações não orientadas a eventos (query, paginação, reprocessamento) e permitir automações externas/integrações que não mantenham conexão WebSocket.

## 2. RealtimeEndpoints (Minimal API)
Exemplo de agrupamento:
```csharp
public static class RealtimeEndpoints
{
    public static void MapRealtimeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/realtime").RequireAuthorization();

        group.MapGet("/workspaces/{workspaceId:guid}/chat", /* histórico */);
        group.MapGet("/notifications", /* listar notificações */);
        group.MapPost("/notifications/{notificationId:guid}/read", /* marcar lida */);
        group.MapGet("/workspaces/{workspaceId:guid}/presence", /* presença */);
    }
}
```

## 3. Controllers

### 3.1 CollaborationController
Opera sobre operações OT (processamento unitário/batch), snapshots e sincronização.
Rotas típicas:
- `POST /api/collaboration/operations`
- `POST /api/collaboration/operations/batch`
- `GET /api/collaboration/workspaces/{workspaceId}/items/{itemId}/snapshot`
- `POST /api/collaboration/synchronize`
- `POST /api/collaboration/conflicts/{conflictId}/resolve`

### 3.2 PresenceController
Gerencia estado de presença via REST (atualização de status, cursores e listagem):
- `GET /api/presence/workspaces/{workspaceId}`
- `PUT /api/presence/status`
- `GET /api/presence/workspaces/{workspaceId}/items/{itemId}/cursors`
- `PUT /api/presence/cursor`

### 3.3 MetricsController
Consulta dashboard e métricas específicas + registro de métricas customizadas:
- `GET /api/metrics/dashboard`
- `GET /api/metrics/{metricName}?from=&to=&workspaceId=`
- `POST /api/metrics/custom`

### 3.4 ChatController
Histórico, envio via REST (fallback, scripts, automações):
- `GET /api/chat/workspaces/{workspaceId}`
- `POST /api/chat/workspaces/{workspaceId}/messages`

## 4. Configuração do SignalR (Program.cs)
Trecho essencial:
```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
})
.AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Serviços
builder.Services.AddScoped<IUserPresenceService, UserPresenceService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// ... métricas, rate limiting, auditoria etc.

var app = builder.Build();
app.MapHub<WorkspaceHub>("/hubs/workspace");
app.MapRealtimeEndpoints();
```

## 5. Estratégias de Versionamento API
| Recurso | Possível Evolução |
|---------|-------------------|
| OT | `/v2/operations` para CRDT ou batch otimizado |
| Métricas | Paginação / agregações pré-calculadas |
| Chat | Filtros por tipo (system, mention) |
| Notificações | Marcações em lote / silent mode |

## 6. Segurança REST
- `[Authorize]` obrigatório em todos grupos.
- Validação de permissão replicada (não confiar apenas em canal SignalR).
- Possível adição de `E-Tag` para snapshots e sincronização condicional.

## 7. Fallback e Estratégia Offline
| Situação | Ação REST |
|----------|-----------|
| Perda de conexão hub | `GET snapshot` + operações subsequentes |
| Recuperar histórico de chat | Paginar via `GET chat` |
| Métricas para observabilidade | `GET metrics` em painéis administrativos |

## 8. Monitoramento de Saúde
Recomendado adicionar (em fase futura — Fase 4):
- `GET /health` (liveness/readiness) com status de DB, Cache, Hub Backplane.
- `GET /metrics/system` (exposição Prometheus) – ou via middleware.

## 9. Boas Práticas de Cliente
| Operação | Prática |
|----------|---------|
| Sincronização | Reenviar última sequência conhecida para minimizar payload |
| Snapshot | Cache local + validação por hash |
| Chat | Usar paginação regressiva (infinite scroll) |
| Cursors | Preferir canal hub; REST apenas debug/admin |

## 10. Próxima Leitura
Finalize em **Parte 10 – Validação, Checklist e Próximos Passos**.

---
_Parte 9 concluída. Próximo: 03-10-validacao-entregaveis.md_
