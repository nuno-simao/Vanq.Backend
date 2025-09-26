# Fase 3 (Parte 8) – Serviços Cross-Cutting (Métricas, Rate Limiting, Auditoria)

> Camada de governança, observabilidade e proteção que dá suporte à colaboração em tempo real.

## 1. Métricas – ICollaborationMetricsService
Rastreamento de contadores, gauges, timers e coleta para dashboards agregados.
```csharp
public interface ICollaborationMetricsService
{
    Task IncrementAsync(string metricName, double value, object tags = null);
    Task RecordLatency(string metricName, TimeSpan latency, object tags = null);
    Task SetGaugeAsync(string metricName, double value, object tags = null);
    Task<List<CollaborationMetrics>> GetMetricsAsync(string metricName, DateTime from, DateTime to, Guid? workspaceId = null);
    Task<CollaborationDashboardData> GetDashboardDataAsync(Guid? workspaceId = null);
    Task CleanupOldMetricsAsync();
}
```
Aspectos:
- `Tags` (JSON) permitem agregar por workspace, item, tipo de operação.
- Dashboard inclui: usuários ativos, throughput de operações, conflitos, latências, violações de limite.
- Pode evoluir para exportação Prometheus / OpenTelemetry.

## 2. Rate Limiting – IRateLimitingService
Protege contra abuso e mantém qualidade de serviço entre planos.
```csharp
public interface IRateLimitingService
{
    Task<bool> CheckLimitAsync(Guid userId, string operation);
    Task<bool> CheckEditLimitAsync(Guid userId, UserPlan userPlan);
    Task<bool> CheckChatLimitAsync(Guid userId, UserPlan userPlan);
    Task<bool> CheckCursorLimitAsync(Guid userId);
    Task<bool> CheckPresenceLimitAsync(Guid userId);
    Task<RateLimitStatus> GetUserLimitStatusAsync(Guid userId);
    Task ResetUserLimitsAsync(Guid userId);
}
```
Características:
- Implementação inicial com `IMemoryCache` (escopo single-node). Escalabilidade futura: Redis.
- Janela deslizante simples por operação (lista de timestamps purgada).
- Métrica `rate_limit_violations` incrementada em excessos.

## 3. Auditoria – ICollaborationAuditService
Registro de eventos críticos para rastreabilidade e compliance.
```csharp
public interface ICollaborationAuditService
{
    Task LogAsync(AuditAction action, string resource, string resourceId, Guid userId, string details = null);
    Task<List<CollaborationAuditLog>> GetAuditLogsAsync(Guid? workspaceId, DateTime? from, DateTime? to, int page = 1, int pageSize = 50);
    Task<List<CollaborationAuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? from, DateTime? to, int page = 1, int pageSize = 50);
    Task CleanupOldAuditLogsAsync();
}
```
Pontos-chave:
- Captura IP, UserAgent e recurso alvo.
- Estratégia de retenção configurável (`COLLABORATION_AUDIT_RETENTION_DAYS`).
- Integrações futuras: SIEM / alerta de segurança.

## 4. Integração Entre Serviços
| Cenário | Fluxo |
|---------|-------|
| Edição | Hub -> RateLimit -> OT -> Métricas + Auditoria |
| Chat | Hub -> RateLimit -> ChatService -> Auditoria + Métricas |
| Conflito | OT -> Métricas + Auditoria -> (Possível intervenção) |
| Desconexão | Hub -> Presença -> Métricas -> Auditoria |

## 5. Métricas Sugeridas (Nomes Convencionais)
| Nome | Tipo | Descrição |
|------|------|-----------|
| edit_operations | Counter | Total de operações aplicadas |
| edit_latency | Timer | Tempo de processamento OT |
| conflicts_detected | Counter | Conflitos identificados |
| conflicts_resolved | Counter | Conflitos resolvidos |
| conflicts_auto_resolved | Counter | Conflitos simples automaticamente resolvidos |
| chat_messages | Counter | Mensagens enviadas |
| cursor_updates | Counter | Atualizações de cursor |
| hub_connections | Counter | Conexões abertas |
| hub_disconnections | Counter | Conexões encerradas |
| rate_limit_violations | Counter | Violações de limite |

## 6. Evoluções Planejáveis
| Área | Evolução |
|------|----------|
| Rate Limiting | Token bucket distribuído com Redis Lua scripts |
| Auditoria | Assinatura digital de logs para não repúdio |
| Métricas | Percentis (p95/p99) para latência de operações |
| Export | OpenTelemetry + traços correlacionados |
| Alertas | Regras: spikes de conflito ou violações |

## 7. Boas Práticas de Observabilidade
- Sempre incluir `workspaceId` e `itemId` em tags quando possível.
- Normalizar nomes de métricas (snake_case) para facilitar queries.
- Limitar cardinalidade de tags (ex: não usar IDs únicos sem agregação planejada).

## 8. Backup & Retenção
| Tipo | Retenção Sugerida | Motivo |
|------|-------------------|--------|
| Métricas brutas | 7 dias | Diagnóstico tático |
| Audit Logs | 30 dias (padrão) | Segurança / compliance |
| Snapshots | 5 dias (padrão) | Reversão rápida / rollback |

## 9. Riscos e Mitigações
| Risco | Mitigação |
|-------|-----------|
| Explosão de memória (rate limiting) | Trocar para Redis / Evict LRU |
| Logs excessivos | Ajustar nível (Info->Warn) em eventos de alta frequência |
| Cardinalidade alta em métricas | Padronizar tags limitadas |
| Auditoria incompleta em falhas | Retry assíncrono / fallback fila |

## 10. Próxima Leitura
Passe para **Parte 9 – Endpoints REST e Configuração da Aplicação**.

---
_Parte 8 concluída. Próximo: 03-09-endpoints-config.md_
