--- 
spec:
  id: SPEC-0006-V2
  type: feature
  version: 2.0.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: low
  quality_order: [security, reliability, observability, performance, delivery_speed, cost]
  tags: [feature-flag, audit, compliance, observability]
  depends_on: [SPEC-0006]
---

# 1. Objetivo
Adicionar auditoria completa ao módulo de feature flags criado no SPEC-0006, persistindo histórico de alterações em tabela dedicada para atender requisitos de compliance (SOX, GDPR, ISO 27001) e facilitar investigação/rollback.

# 2. Escopo
## 2.1 In
- Entidade `FeatureFlagAuditLog` para armazenar histórico completo de mudanças.
- Trigger/interceptor automático que registra toda alteração em `FeatureFlag`.
- Endpoints administrativos para consultar histórico (`/admin/feature-flags/{key}/audit`).
- Retenção configurável de logs de auditoria (ex.: 90 dias, 1 ano).
- Migração de logs estruturados existentes (importação opcional).

## 2.2 Out
- Auditoria de consultas (apenas de alterações).
- UI completa de visualização de histórico (foco em API).
- Integração com SIEM externos nesta fase.

## 2.3 Não Fazer
- Alterar funcionamento da entidade `FeatureFlag` ou serviços existentes.
- Implementar auditoria em tempo real (batch diário suficiente para compliance).

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|--------------------------------|
| REQ-01 | Persistir em tabela dedicada toda alteração de feature flag com valores old/new. | MUST |
| REQ-02 | Capturar contexto completo: usuário, timestamp, IP, motivo, correlation ID. | MUST |
| REQ-03 | Disponibilizar endpoint para consultar histórico de um flag específico (paginado). | MUST |
| REQ-04 | Suportar filtros: intervalo de datas, usuário, ambiente. | SHOULD |
| REQ-05 | Implementar política de retenção configurável (ex.: deletar registros > 365 dias). | SHOULD |
| REQ-06 | Oferecer comando/endpoint para exportar auditoria em formato CSV/JSON. | MAY |
| REQ-07 | Permitir importação de logs estruturados pré-existentes para popular histórico inicial. | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | Inserção de audit log não deve bloquear atualização de flag (async). | < 5ms overhead |
| NFR-02 | Segurança | Endpoint de consulta restrito a roles auditores/admins; dados imutáveis. | Política configurada |
| NFR-03 | Confiabilidade | Falha ao gravar audit log não deve impedir operação principal (log error). | Resiliência testada |
| NFR-04 | Observabilidade | Métricas de auditoria: `audit_log_insert_total`, `audit_log_query_total`. | Prometheus exporta |
| NFR-05 | Compliance | Registros imutáveis; sem DELETE permitido (apenas soft delete/archival). | Constraint DB |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Todo registro de auditoria é imutável após criação (sem UPDATE/DELETE direto). |
| BR-02 | Política de retenção aplica-se apenas a registros não críticos (`IsCritical = false`). |
| BR-03 | Exportação de auditoria requer aprovação dupla ou MFA para flags críticos. |
| BR-04 | Auditoria deve preservar dados mesmo se flag original for deletado (soft delete). |

# 6. Novas Entidades
| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | FeatureFlagAuditLog | Armazenar histórico completo de alterações de feature flags. | Append-only; índices em timestamp e key. |

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| FeatureFlagAuditLog | Id | Guid | Não | PK |
| FeatureFlagAuditLog | FeatureFlagId | Guid | Não | FK para FeatureFlag |
| FeatureFlagAuditLog | Key | string(128) | Não | Desnormalizado para queries |
| FeatureFlagAuditLog | Environment | string(50) | Não | Desnormalizado |
| FeatureFlagAuditLog | OldValue | bool | Não | Estado anterior |
| FeatureFlagAuditLog | NewValue | bool | Não | Estado novo |
| FeatureFlagAuditLog | ChangedBy | string(64) | Não | UserId ou "system" |
| FeatureFlagAuditLog | ChangedAt | DateTime (UTC) | Não | Timestamp da mudança |
| FeatureFlagAuditLog | Reason | string(512) | Sim | Motivo da alteração |
| FeatureFlagAuditLog | IpAddress | string(45) | Sim | IPv4/IPv6 |
| FeatureFlagAuditLog | CorrelationId | Guid | Sim | Para rastreamento |
| FeatureFlagAuditLog | Metadata | jsonb/text | Sim | Contexto adicional (user agent, etc.) |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nova entidade `FeatureFlagAuditLog` com constraint imutável. | Value object para `AuditLogEntry`. |
| Application | Interface `IFeatureFlagAuditRepository`; serviço `FeatureFlagAuditService`. | Métodos: `LogChangeAsync`, `GetHistoryAsync`. |
| Infrastructure | Repositório EF Core; interceptor `AuditInterceptor` ou domain event handler. | Configuração EF: índices compostos, retention policy job. |
| API | Endpoints `/admin/feature-flags/{key}/audit` (GET, exportação). | Autorização via `RequirePermission("audit:read")`. |

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /admin/feature-flags/{key}/audit | JWT + audit role | REQ-03 | 200 paged list | 401,403,404 |
| API-02 | GET | /admin/feature-flags/{key}/audit/export | JWT + audit role | REQ-06 | 200 CSV/JSON | 401,403,404 |
| API-03 | GET | /admin/feature-flags/audit/stats | JWT + admin | REQ-04 | 200 stats DTO | 401,403 |

## 8.1 Exemplo de Resposta (API-01)
```json
{
  "key": "rbac-enabled",
  "environment": "Production",
  "totalChanges": 12,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "...",
      "oldValue": false,
      "newValue": true,
      "changedBy": "admin@vanq.io",
      "changedAt": "2025-09-30T14:32:10Z",
      "reason": "Enabling RBAC for production rollout",
      "ipAddress": "203.0.113.42",
      "correlationId": "..."
    }
  ]
}
```

# 9. Segurança & Performance
- **Segurança**: Endpoint requer role `auditor` ou `admin`; registros imutáveis (constraint FK + trigger prevents UPDATE/DELETE); exportação loga acesso para SIEM.
- **Performance**: Usar async/background job para gravar auditoria; índice composto em `(Key, Environment, ChangedAt)`; considerar particionamento por mês se volume > 1M registros.
- **Observabilidade**: Métrica `audit_log_retention_deleted_total`; alerta se inserção falhar > 1% do tempo.

# 10. i18n
Não aplicável (dados técnicos de auditoria). Campos `Reason` podem aceitar múltiplos idiomas futuramente.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | feature-flags-audit-enabled | Infra | Habilita gravação de auditoria (kill switch). | Desligado → apenas logs estruturados |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Criar entidade `FeatureFlagAuditLog` + configuração EF (índices, constraint imutável). | - | REQ-01 |
| TASK-02 | Implementar repositório `FeatureFlagAuditRepository` com métodos de consulta paginada. | TASK-01 | REQ-03 |
| TASK-03 | Criar interceptor/event handler para capturar mudanças automaticamente. | TASK-01 | REQ-01,REQ-02 |
| TASK-04 | Desenvolver endpoints de consulta com filtros (data, usuário, ambiente). | TASK-02 | REQ-03,REQ-04 |
| TASK-05 | Implementar exportação CSV/JSON com streaming para grandes volumes. | TASK-02 | REQ-06 |
| TASK-06 | Criar background job para política de retenção (cleanup de registros antigos). | TASK-01 | REQ-05 |
| TASK-07 | Script de importação opcional de logs estruturados pré-existentes. | TASK-01 | REQ-07 |
| TASK-08 | Adicionar métricas Prometheus e logs estruturados para operações de auditoria. | TASK-03 | NFR-04 |
| TASK-09 | Testes (unit/integration) cobrindo imutabilidade, paginação, retenção. | TASK-01..06 | NFR-02,NFR-03 |
| TASK-10 | Documentar API de auditoria e processo de exportação para compliance. | TASK-04,05 | REQ-03 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Toda alteração em `FeatureFlag` gera registro em `FeatureFlagAuditLog` com old/new values. |
| REQ-02 | Auditoria captura userId, timestamp UTC, IP, reason (se fornecido), correlationId. |
| REQ-03 | Endpoint retorna histórico paginado ordenado por `ChangedAt DESC`. |
| REQ-04 | Filtros por intervalo de datas e usuário funcionam corretamente. |
| REQ-05 | Job de retenção deleta registros > 365 dias (configurável) sem impactar flags críticos. |
| REQ-06 | Exportação CSV contém todas colunas e suporta arquivos > 10k registros via streaming. |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-01 | Atualização de flag cria registro de auditoria automaticamente. |
| TEST-02 | Unit | REQ-02 | Contexto completo (IP, reason, correlationId) é capturado corretamente. |
| TEST-03 | Integration | REQ-03 | Endpoint de histórico retorna paginação correta. |
| TEST-04 | Unit | REQ-04 | Filtros de data e usuário funcionam isoladamente e combinados. |
| TEST-05 | Integration | REQ-05 | Job de retenção deleta apenas registros elegíveis (não críticos + antigos). |
| TEST-06 | Unit | NFR-05 | Tentativa de UPDATE/DELETE em audit log falha com constraint violation. |
| TEST-07 | Integration | NFR-03 | Falha ao gravar auditoria não bloqueia operação principal. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Captura automática | Usar EF Core interceptor ou domain events | Trigger SQL | Mantém lógica em C#; testável; portável entre DBs |
| DEC-02 | Imutabilidade | Constraint DB + validação aplicação | Apenas validação aplicação | Garantia em múltiplas camadas contra corrupção |
| DEC-03 | Performance | Gravação assíncrona com retry | Síncrono bloqueante | Não impacta latência de alteração de flags |
| DEC-04 | Retenção | Background job agendado (ex.: daily 2am) | Trigger automático on-insert | Controle explícito e observável |
| DEC-05 | Exportação | Streaming para grandes volumes | Carregar tudo em memória | Suporta datasets > RAM disponível |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Qual período de retenção padrão (90, 180, 365 dias)? | owner/compliance | Aberto |
| QST-02 | Precisamos integrar com SIEM externo (Splunk, ELK) já nesta fase? | owner/security | Aberto |
| QST-03 | Exportação deve ser síncrona (streaming) ou assíncrona (job + download)? | owner | Aberto |

# 17. Contexto de Implementação Existente

## 17.1 SPEC-0006 (Versão Atual)
O SPEC-0006 V1 implementa feature flags com **auditoria básica**:
- Campos `LastUpdatedBy`, `LastUpdatedAt`, `Metadata` na entidade `FeatureFlag`
- Logs estruturados (Serilog/ILogger) com contexto completo
- Sem tabela de histórico dedicada

**Limitações da V1:**
- Histórico disponível apenas enquanto logs estiverem retidos
- Consultas via agregadores externos (Seq, ELK)
- Não atende requisitos de compliance que exigem persistência imutável

## 17.2 Motivação para V2
Empresas que precisam de:
- **Compliance regulatório**: SOX (Sarbanes-Oxley), GDPR, ISO 27001
- **Auditoria forense**: investigar incidentes com histórico completo
- **Rollback auditado**: saber exatamente quem mudou o quê e quando
- **Exportação para auditorias**: fornecer CSVs para auditores externos

## 17.3 Estratégia de Migração
1. SPEC-0006 V1 funciona normalmente sem V2 (independente)
2. V2 é **aditivo**: não altera comportamento existente
3. Após deploy V2, habilitar flag `feature-flags-audit-enabled`
4. Opcionalmente importar logs históricos para popular auditoria inicial

# 18. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0006-V2 criando módulo de auditoria completa para feature flags. Adicionar entidade `FeatureFlagAuditLog` (imutável), interceptor automático de mudanças, endpoints administrativos de consulta/exportação, background job de retenção. Usar async para não bloquear operações principais. Restringir acesso a roles `auditor`/`admin`. Garantir imutabilidade via constraint DB. Seguir padrão do SPEC-0006 V1 sem alterá-lo. Considerar particionamento/índices para performance em alto volume. Adicionar métricas Prometheus.

Fim.
