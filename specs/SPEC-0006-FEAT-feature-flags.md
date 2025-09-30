---
spec:
  id: SPEC-0006
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [reliability, delivery_speed, observability, performance, security, cost]
  tags: [feature-flag, configuration, platform]
---

# 1. Objetivo
Disponibilizar um módulo de feature flags nativo, sem dependências externas, permitindo habilitar/desabilitar funcionalidades dinamicamente via banco de dados, com cache de leitura e invalidação automática quando um flag for alterado.

# 2. Escopo
## 2.1 In
- Estrutura de dados (tabela) para armazenar flags com chave, descrição, estado, ambiente e metadados.
- Serviços de aplicação/interna para consultar, atualizar e listar flags.
- Cache em memória para leituras rápidas com invalidação on-change.
- API interna (ou comandos administrativos) para atualizar flags.
- Integração com endpoints existentes via `IFeatureFlagService` (ex.: registro de usuário `user-registration-enabled`).
- Documentação de uso e exemplos.

## 2.2 Out
- UI de administração completa (foco inicial em comandos/API mínima).
- Suporte a targeting avançado (por usuário, porcentagem, scheduling).
- Integração com sistemas externos de configuração (Azure App Config, LaunchDarkly, etc.).

## 2.3 Não Fazer
- Carregar flags via arquivos no runtime; persistência deve ser exclusivamente pelo banco.
- Implementar auditoria completa nesta fase (apenas registrar campos básicos de auditoria).

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Persistir feature flags em tabela dedicada com chave única por ambiente. | MUST |
| REQ-02 | Expor serviço `IFeatureFlagService` para consultar flag por chave com cache em memória. | MUST |
| REQ-03 | Disponibilizar operação para criar/atualizar flag que persista em banco e invalide cache. | MUST |
| REQ-04 | Suportar ambientes (ex.: Development, Staging, Production) permitindo valores diferentes por ambiente. | MUST |
| REQ-05 | Disponibilizar endpoint/command seguro para gerenciar flags (list, update, toggle). | SHOULD |
| REQ-06 | Registrar eventos/logs estruturados ao alterar flag, incluindo usuário/responsável. | SHOULD |
| REQ-07 | Permitir adicionar metadados simples (descrição, responsável, data atualização). | SHOULD |
| REQ-08 | Oferecer método de verificação com fallback (`GetFlagOrDefaultAsync`) para evitar falhas quando flag não existe. | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | Consulta de flag após cache frio < 10ms; cache quente ~O(1). | Benchmarks dev |
| NFR-02 | Confiabilidade | Invalidação de cache deve ocorrer imediatamente após alteração. | Teste confirma `< 1s` propagação |
| NFR-03 | Observabilidade | Logar toda alteração com contexto (flag, valor, usuário, motivo). | 100% alterações |
| NFR-04 | Segurança | Endpoint de gerenciamento requer autenticação/role apropriada. | Política de autorização configurada |
| NFR-05 | Resiliência | Em caso de falha no cache, serviço deve voltar ao banco sem quebrar fluxo. | Teste simula perda de cache |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Chave do flag deve ser única por ambiente e seguir convenção `kebab-case` (ex.: `user-registration-enabled`). |
| BR-02 | Flags críticos (ex.: auth) devem ter metadata `IsCritical = true` e exigir confirmação dupla na alteração. |
| BR-03 | Flags sem entrada explícita retornam valor default configurado (false por padrão) para evitar comportamento inesperado. |

# 6. Novas Entidades
| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | FeatureFlag | Representar estado de uma feature por ambiente. | Incluir auditoria básica. |

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| FeatureFlag | Id | Guid | Não | PK |
| FeatureFlag | Key | string(128) | Não | Único por Ambiente |
| FeatureFlag | Environment | string(50) | Não | Enum ou string validada |
| FeatureFlag | IsEnabled | bool | Não | |
| FeatureFlag | Description | string(256) | Sim | |
| FeatureFlag | IsCritical | bool | Não | Default false |
| FeatureFlag | LastUpdatedBy | string(64) | Sim | Registrado via contexto |
| FeatureFlag | LastUpdatedAt | DateTime (UTC) | Não | Default now |
| FeatureFlag | Metadata | jsonb/text | Sim | Campos adicionais (responsável, motivo) |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nova entidade `FeatureFlag` com invariantes (ex.: key/env). | Validar construtor.
| Application | Interfaces `IFeatureFlagService`, `IFeatureFlagRepository`; DTOs para gestão. | Uso em serviços existentes.
| Infrastructure | Nova tabela + repositório EF Core; configuração `FeatureFlagConfiguration`. | Cache via `IMemoryCache` ou custom; invalidar com eventos.
| API | Endpoints administrativos (ex.: `/admin/feature-flags`) e middleware para resolver ambiente. | Autorizar via role.

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /admin/feature-flags | JWT + role admin | REQ-05 | 200 lista DTO | 401,403 |
| API-02 | PUT | /admin/feature-flags/{key} | JWT + role admin | REQ-03,REQ-05 | 200 flag atualizado | 400,401,403,404 |
| API-03 | POST | /admin/feature-flags | JWT + role admin | REQ-03,REQ-05 | 201 criado | 400,401,403,409 |

# 9. Segurança & Performance
- Segurança: restringir endpoints a administradores; logar usuário responsável; validar inputs para evitar injeção (ex.: key, metadata).
- Performance: usar cache com timeout curto (ex.: 60s) + invalidação ativa após update; fallback para banco quando cache falhar.
- Observabilidade: expor métrica `feature_flag_toggle_total` com labels (`key`, `environment`, `status`).

# 10. i18n
Não aplicável (mensagens administrativas técnicas). Mensagens podem permanecer em en-US inicialmente.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | feature-flags-enabled | Infra | Possibilita desligar leitura do módulo (fallback: todas features habilitadas). | Desligado → retorna default true |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Criar entidade `FeatureFlag` + configuração EF (índices, constraints). | - | REQ-01 |
| TASK-02 | Adicionar repositório e serviço de aplicação com cache (`IMemoryCache`). | TASK-01 | REQ-02,REQ-03 |
| TASK-03 | Implementar estratégia de invalidação (ex.: CacheKey por key/env) acionada após update. | TASK-02 | REQ-03 |
| TASK-04 | Desenvolver endpoints/admin commands autenticados. | TASK-02 | REQ-05,REQ-06 |
| TASK-05 | Integrar com feature existente (`user-registration-enabled`). | TASK-02 | REQ-02 |
| TASK-06 | Adicionar logging estruturado e métricas para alterações. | TASK-02 | NFR-03 |
| TASK-07 | Criar testes (unit/integration) cobrindo cache, fallback, concorrência. | TASK-01..03 | NFR-02,REQ-02 |
| TASK-08 | Documentar uso (README/ops) com instruções de criação/alteração. | TASK-04 | REQ-05 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Banco possui tabela `FeatureFlags` com índice único `(Key, Environment)`.
| REQ-02 | Consulta repetida do mesmo flag em ambiente saudável não acessa o banco (cache hit).
| REQ-03 | Atualização via API reflete imediatamente na próxima consulta (cache invalidado).
| REQ-04 | Flag pode ter valores distintos por ambiente e consulta retorna valor correto.
| REQ-05 | Endpoint protegido exige role/admin; resposta lista flags existentes.
| REQ-06 | Logs exibem `event=FeatureFlagChanged` com detalhes (key, from, to, user).

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-02 | Verifica cache hit após primeira consulta. |
| TEST-02 | Unit | REQ-03 | Atualização invalida cache e retorna novo valor. |
| TEST-03 | Integration | REQ-05 | Endpoint PUT atualiza flag com autenticação válida. |
| TEST-04 | Integration | REQ-04 | Flags por ambiente retornam valores distintos. |
| TEST-05 | Unit | NFR-05 | Falha de cache leva a fallback para banco sem exceção. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Persistência | Usar EF Core + tabela `FeatureFlags` | Config files | Coerência com stack atual |
| DEC-02 | Cache | `IMemoryCache` com invalidation manual | Redis/local cache | Simplicidade inicial |
| DEC-03 | Autorização | Restringir endpoints a role `admin` | API aberta | Protege toggles críticos |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Precisamos de histórico/auditoria completo ou basta log estruturado? | owner | Aberto |
| QST-02 | Quais flags iniciais devem ser cadastrados automaticamente (seed)? | owner | Aberto |
| QST-03 | Como será definido o ambiente atual (header, config, claims)? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0006 criando módulo de feature flags persistido em banco com cache em memória e invalidação on-change, endpoints administrativos protegidos, logging e métricas. Integrar com flags existentes (`user-registration-enabled`). Não usar bibliotecas externas de feature toggles.

Fim.
