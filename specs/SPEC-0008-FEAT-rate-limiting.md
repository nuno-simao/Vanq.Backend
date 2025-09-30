---
spec:
  id: SPEC-0008
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [security, reliability, performance, observability, delivery_speed, cost]
  tags: [rate-limiting, security, api]
---

# 1. Objetivo
Introduzir rate limiting na Vanq API para proteger recursos críticos contra abuso, controlar consumo por cliente e garantir disponibilidade, utilizando a stack nativa do ASP.NET Core sem bibliotecas externas.

# 2. Escopo
## 2.1 In
- Configurar rate limiting por endpoint/grupo (ex.: `/auth/*` com limites específicos).
- Suporte a políticas diferenciadas por tipo de cliente (usuário autenticado vs anônimo, IP vs API key).
- Persistência opcional de contadores em memória/distribuído (inicialmente memória local, com extensibilidade futura).
- Expor métricas e logs de bloqueios/throttling.
- Endpoint administrativo para consultar estado básico (contadores ativos/opções).
- Documentação de configuração e uso em ambientes diferentes.

## 2.2 Out
- Persistência distribuída (Redis, SQL) nesta fase; planejar expansão futura.
- Rate limiting adaptativo/algoritmos avançados (token bucket personalizado com machine learning, etc.).
- UI de administração.

## 2.3 Não Fazer
- Implementar throttling por usuário em background (foco em middleware HTTP).
- Alterar fluxos de autenticação além do necessário para extrair identificadores.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Registrar políticas de rate limiting usando `AddRateLimiter` com nomes configuráveis. | MUST |
| REQ-02 | Definir política padrão global (ex.: 100 req/min por IP) e política específica para `/auth/*` (ex.: 10 req/min). | MUST |
| REQ-03 | Permitir identificar consumidor via API Key (header) ou usuário autenticado, caindo para IP quando não identificável. | MUST |
| REQ-04 | Retornar resposta HTTP 429 com corpo padronizado (Problem Details) e cabeçalhos `Retry-After`. | MUST |
| REQ-05 | Expor métricas/logs para contagem de bloqueios e requisições limitadas. | SHOULD |
| REQ-06 | Permitir override por ambiente/config (ex.: limites mais altos em dev). | SHOULD |
| REQ-07 | Disponibilizar endpoint/command para consultar configuração ativa e estatísticas básicas. | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | Overhead do middleware < 3ms p95. | Benchmarks dev |
| NFR-02 | Observabilidade | 100% dos bloqueios registrados com `event=RateLimitTriggered`. | Auditoria |
| NFR-03 | Confiabilidade | Rate limiting deve ser configurável por ambiente e desativável via feature flag. | Teste config |
| NFR-04 | Segurança | Evitar bypass; usar identificador mais específico quando disponível (API Key/Usuário). | Testes |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Limites para `/auth/login` e `/auth/register` mais restritivos que padrão (proteção contra brute-force).
| BR-02 | Consumidores com API Key válida podem ter limites diferenciados configurados.
| BR-03 | Feature flag `rate-limiting-enabled` pode desligar temporariamente o middleware (fallback: sem limitação).

# 6. Novas Entidades
Nenhuma entidade de domínio nova; podem haver DTOs/configurações.

| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| - | - | - | - |

## 6.1 Campos (Somente Entidades Novas)
(N/A)

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nenhum impacto direto. | |
| Application | Adicionar contrato `IRateLimitPolicyProvider` se necessário para centralizar lógica de identificação. | Pode integrar com feature flags.
| Infrastructure | Configurar rate limiting em `ServiceCollectionExtensions`; usar `PartitionedRateLimiter` baseado em memória. | Preparar para swap por storage distribuído futuro.
| API | Ajustar `Program.cs` para adicionar middleware `UseRateLimiter`; criar Problem Details custom para 429. | Atualizar documentação Scalar.

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /admin/rate-limits | JWT + role admin | REQ-07 | 200 config atual | 401,403 |

# 9. Segurança & Performance
- Segurança: proteger endpoints críticos com limites mais baixos; logar tentativas excessivas; permitir bloqueios temporários (lista negra manual se necessário no futuro).
- Performance: usar rate limiter nativo com armazenamento em memória para minimizar latência; medir overhead em ambiente dev.
- Observabilidade: métricas `rate_limit_trigger_total` com labels (`policy`, `route`, `identifier`), logs estruturados e dashboards.

# 10. i18n
Mensagens de resposta podem usar Problem Details com mensagem padrão em en-US; optional i18n futura.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | rate-limiting-enabled | API | Kill switch global | Desligado → middleware bypass |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Definir configurações (`RateLimitingOptions`) em `appsettings` (limites por política, mensagens). | - | REQ-02,REQ-06 |
| TASK-02 | Registrar `AddRateLimiter` com políticas (global, auth, API key). | TASK-01 | REQ-01..03 |
| TASK-03 | Implementar identificador de consumidor (API key/Claims/IP). | TASK-02 | REQ-03 |
| TASK-04 | Customizar resposta 429 (Problem Details + Retry-After). | TASK-02 | REQ-04 |
| TASK-05 | Integrar feature flag `rate-limiting-enabled`. | TASK-02 | BR-03 |
| TASK-06 | Adicionar métricas/logs para bloqueios e contadores. | TASK-02 | REQ-05 |
| TASK-07 | Criar endpoint/admin command de consulta. | TASK-02 | REQ-07 |
| TASK-08 | Documentar configuração e uso (README/ops + Scalar). | TASK-06 | REQ-05 |
| TASK-09 | Implementar testes unitários/integrados (limite atingido, resets). | TASK-02 | NFR-01..04 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Policies registradas e ativas conforme configuração.
| REQ-02 | `/auth/login` bloqueia após atingir limite configurado retornando 429.
| REQ-03 | Usuário autenticado conta separadamente de IP anônimo.
| REQ-04 | Resposta 429 inclui Problem Details com `type`, `title`, `detail`, `retryAfter`.
| REQ-05 | Métricas e logs registram bloqueios com contexto.
| REQ-06 | Ambiente dev/staging pode ajustar limites via `appsettings`. |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-02 | Simula excesso no `/auth/login` e valida 429.
| TEST-02 | Integration | REQ-03 | Usuário autenticado e anônimo possuem contadores independentes.
| TEST-03 | Unit | REQ-04 | Verifica Problem Details customizado.
| TEST-04 | Unit | REQ-05 | Logger captura `event=RateLimitTriggered`.
| TEST-05 | Integration | REQ-06 | Config diferente por ambiente altera limites.

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Biblioteca | Usar rate limiting nativo `Microsoft.AspNetCore.RateLimiting`. | Lib externa (AspNetCoreRateLimit) | Menos dependências externas.
| DEC-02 | Armazenamento | In-memory inicialmente, com possibilidade de extensão. | Redis imediato | Menos setup inicial.
| DEC-03 | Identificador | Preferir API key/usuário; fallback IP. | Somente IP | Melhor precisão por cliente.

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Quais endpoints além de `/auth/*` precisam políticas custom? | owner | Aberto |
| QST-02 | Precisamos implementar whitelist para IPs internos? | owner | Aberto |
| QST-03 | Qual estratégia para reset de contadores em ambientes de longa duração? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0008 configurando rate limiting nativo com políticas global e específicas para `/auth/*`, identificação por usuário/API key/IP, resposta 429 customizada, métricas/logs e feature flag `rate-limiting-enabled`. Garantir documentação e testes cobrindo limites e observabilidade.

Fim.
