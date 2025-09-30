---
spec:
  id: SPEC-0004
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: medium
  quality_order: [reliability, observability, performance, security, delivery_speed, cost]
  tags: [api, health-check, monitoring, ops]
---

# 1. Objetivo
Expor health checks padronizados na Vanq API para monitorar disponibilidade do ambiente e do banco de dados, permitindo integração com ferramentas de observabilidade e pipelines de deploy.

# 2. Escopo
## 2.1 In
- Adicionar suporte a `Microsoft.Extensions.Diagnostics.HealthChecks` na API.
- Criar endpoint `/health` (liveness) e `/health/ready` (readiness) com retorno JSON padronizado.
- Incluir verificação do banco de dados PostgreSQL via cadeia de conexão atual.
- Verificar presença/validade das variáveis de ambiente críticas (ex.: `Jwt:SigningKey`, `ConnectionStrings:DefaultConnection`).
- Emitir métricas/logs estruturados para status dos health checks.
- Documentar uso (incluindo como consultar endpoints e interpretar respostas) no README/Scalar.

## 2.2 Out
- Implementar health checks para serviços externos adicionais (ex.: storage, messaging) além de DB e env.
- Integrar com Kubernetes/LB (apenas fornecer endpoints; configuração externa é responsabilidade do ambiente).
- Automação de alertas (fica para etapa de observabilidade avançada).

## 2.3 Não Fazer
- Expor informações sensíveis (strings de conexão completas, segredos) nas respostas.
- Habilitar escrita/diagnóstico avançado via health check (somente leitura).

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Registrar health checks com política nomeada (`vanq-health`) incluindo verificação de banco PostgreSQL. | MUST |
| REQ-02 | Criar endpoint `/health` (liveness) respondendo 200 quando o host está ativo. | MUST |
| REQ-03 | Criar endpoint `/health/ready` agregando checagens de banco e ambiente, respondendo 503 quando alguma falhar. | MUST |
| REQ-04 | Validar variáveis de ambiente críticas e refletir status em health check de readiness. | MUST |
| REQ-05 | Retornar payload JSON com status, duração e detalhes das checagens. | SHOULD |
| REQ-06 | Configurar cache/timeout adequados (ex.: timeout 3s para DB). | SHOULD |
| REQ-07 | Permitir autenticação opcional (ex.: header chave) configurável via appsettings. | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Observabilidade | Logar resultado das execuções em nível `Information` quando status = `Unhealthy` ou `Degraded`. | 100% dos eventos negativos logados |
| NFR-02 | Performance | Cada health check deve responder em < 500ms p95 em ambiente saudável. | Medição dev/QA |
| NFR-03 | Segurança | Respostas não devem expor segredos; somente `status` e mensagens genéricas. | Revisão de payload |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Health check de readiness deve considerar o ambiente atual (prod/qa/dev) para validar variáveis específicas. |
| BR-02 | Falha no banco deve retornar mensagem genérica "Database connection failed" com detalhes apenas no log. |
| BR-03 | Lista de variáveis críticas deve ser configurável via `appsettings` seção `HealthChecks:Environment:RequiredVariables`. |

# 6. Novas Entidades
Nenhuma entidade de domínio adicional.

| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| - | - | - | - |

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| - | - | - | - | - |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nenhuma. | |
| Application | Potencial adição de abstrações para checagens customizadas reutilizáveis. | Avaliar criação de `IEnvironmentHealthChecker`. |
| Infrastructure | Uso de `IHealthChecksBuilder` com Npgsql (dependência já existente). | Configurar connection pooling e timeout. |
| API | Configuração em `Program.cs` (`AddHealthChecks`, `MapHealthChecks`) e possível extensão para formatação custom. | Atualizar documentação Scalar. |

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /health | Opcional (sem auth por padrão) | REQ-02 | 200 `{ "status": "Healthy" }` | 503 |
| API-02 | GET | /health/ready | Opcional | REQ-03, REQ-04 | 200 `{ "status": "Healthy", "entries": { ... } }` | 503 com detalhes |

# 9. Segurança & Performance
- Segurança: considerar habilitar auth (header compartilhado) para ambientes fora de cluster confiável; mascarar mensagens sensíveis.
- Performance: usar timeout e `HealthCheckOptions.ResponseWriter` eficiente; evitar checagens pesadas.
- Observabilidade: integrar com logs estruturados e (futuro) métricas Prometheus/Azure Monitor.

# 10. i18n
Não aplicável (respostas técnicas).

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | health-checks-enabled | API | Permite desligar endpoints temporariamente em caso de incidentes ou migração. | Desligado → endpoints retornam 404 |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Adicionar seção `HealthChecks` em `appsettings` com variáveis/timeout/opções. | - | REQ-01,REQ-04 |
| TASK-02 | Registrar serviços de health check (`AddHealthChecks`) incluindo check de DB Npgsql. | TASK-01 | REQ-01 |
| TASK-03 | Implementar health check custom de variáveis de ambiente. | TASK-01 | REQ-04 |
| TASK-04 | Mapear endpoints `/health` e `/health/ready` com writer custom. | TASK-02 | REQ-02,REQ-03,REQ-05 |
| TASK-05 | Atualizar documentação (README/Scalar) com instruções de monitoramento. | TASK-04 | REQ-05 |
| TASK-06 | Criar testes de integração validando respostas Healthy/Unhealthy/Degraded. | TASK-04 | REQ-02..05 |
| TASK-07 | Incluir logging/observabilidade para eventos de falha. | TASK-02 | NFR-01 |
| TASK-08 | Ajustar feature flag `health-checks-enabled`. | TASK-04 | REQ-07 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Health check de DB reporta `Healthy` com banco disponível e `Unhealthy` quando indisponível. |
| REQ-02 | `/health` retorna 200 com payload liveness simples quando API responde. |
| REQ-03 | `/health/ready` retorna 503 quando DB ou variáveis estão inválidos. |
| REQ-04 | Ambiente com variável ausente mostra entrada `Unhealthy` com mensagem genérica. |
| REQ-05 | Payload JSON inclui `status`, `totalDuration`, `entries`. |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-01,REQ-03 | Simula banco indisponível e valida resposta `Unhealthy`. |
| TEST-02 | Integration | REQ-02 | `/health` retorna 200 em cenário saudável. |
| TEST-03 | Unit | REQ-04 | Health check de variáveis falha quando variável crítica ausente. |
| TEST-04 | Integration | REQ-05 | Verifica estrutura do payload JSON customizado. |
| TEST-05 | Unit | NFR-01 | Garante logging de eventos `Unhealthy`. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Formato de resposta | Utilizar `HealthCheckOptions.ResponseWriter` custom baseado em JSON | Resposta padrão plaintext | Melhora integração com observabilidade. |
| DEC-02 | Nome dos endpoints | `/health` e `/health/ready` | `/healthz`, `/live` | Compatível com padrões Kubernetes/ASP.NET. |
| DEC-03 | Estrutura de configuração | `appsettings` com override por env vars | Código hardcoded | Flexibilidade e aderência 12-factor. |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Quais variáveis além de `Jwt:SigningKey` e `ConnectionStrings:DefaultConnection` são críticas? | owner | Aberto |
| QST-02 | Necessário autenticar endpoints em produção? | owner | Aberto |
| QST-03 | Precisamos reportar `Degraded` separadamente (ex.: latência alta) nesta fase? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0004 adicionando health checks para liveness e readiness com verificação do PostgreSQL e das variáveis de ambiente críticas, configuráveis via `appsettings`, trazendo logging e documentação. Respeitar feature flag `health-checks-enabled` e garantir payload JSON padronizado.

Fim.
