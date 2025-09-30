---
spec:
  id: SPEC-0007
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: medium
  quality_order: [reliability, observability, performance, security, delivery_speed, cost]
  tags: [configuration, system-params, platform]
---

# 1. Objetivo
Permitir que a Vanq API armazene e recupere parâmetros de sistema (chave/valor) diretamente do banco de dados, com cache em memória e invalidação automática, eliminando dependência de bibliotecas externas e facilitando ajustes dinâmicos de configuração.

# 2. Escopo
## 2.1 In
- Criação de entidade/tabela `SystemParameter` para persistir chaves e valores.
- Serviço de leitura/escrita com cache em memória e fallback para banco.
- Suporte a tipos de parâmetros (string, número, bool, json) com conversão e validação.
- Invalidação automática do cache quando um parâmetro é atualizado.
- API ou comandos administrativos para listar/atualizar parâmetros com segurança.
- Documentação de uso e exemplos.

## 2.2 Out
- Tipagem dinâmica avançada (ex.: coerção automática para classes complexas além de json simples).
- Integração com sistemas externos de configuração/secret manager.
- UI completa de gestão (foco inicial em endpoints/CLI).

## 2.3 Não Fazer
- Carregar parâmetros por arquivo no runtime; persistência central é o banco.
- Expor valores sensíveis sem mascaramento (apenas retorno controlado).

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Persistir parâmetros em tabela dedicada com chave única. | MUST |
| REQ-02 | Disponibilizar serviço `ISystemParameterService` para obter valor com cache (GetAsync/GetValue<T>). | MUST |
| REQ-03 | Permitir criação/atualização de parâmetros, persistindo no banco e invalidando cache. | MUST |
| REQ-04 | Suportar múltiplos tipos primários (string, int, decimal, bool, json) com validação. | SHOULD |
| REQ-05 | Oferecer mecanismo para retornar valor default quando parâmetro ausente. | SHOULD |
| REQ-06 | Disponibilizar endpoint/admin command protegido para gerenciar parâmetros. | SHOULD |
| REQ-07 | Registrar logs estruturados e metadados de auditoria (quem alterou, quando). | SHOULD |
| REQ-08 | Permitir agrupar parâmetros por categoria/namespace (ex.: `auth`, `notifications`). | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | Consulta após cache frio < 15ms; cache quente ~O(1). | Bench dev |
| NFR-02 | Confiabilidade | Invalidação de cache deve ocorrer em até 1s após alteração. | Teste integração |
| NFR-03 | Observabilidade | Logar alterações com contexto (param, valor anterior/novo, usuário). | 100% eventos |
| NFR-04 | Segurança | Endpoints de gestão exigem role específica; valores sensíveis mascarados nas respostas. | Teste autorização |
| NFR-05 | Resiliência | Falha no cache não deve causar indisponibilidade; serviço recorre ao banco. | Teste fallback |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Chaves devem seguir convenção `dot.case` (ex.: `auth.password.minLength`). |
| BR-02 | Parâmetros críticos marcam metadata `IsSensitive=true` e retornam mascarados em listagens. |
| BR-03 | Atualizações exigem registro do usuário responsável e justificativa (campo `Reason`). |

# 6. Novas Entidades
| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | SystemParameter | Representar chave/valor configurável do sistema. | Incluir metadata e auditoria. |

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| SystemParameter | Id | Guid | Não | PK |
| SystemParameter | Key | string(150) | Não | Único |
| SystemParameter | Value | string | Não | Conteúdo em formato string/json |
| SystemParameter | Type | string(20) | Não | Enum `string|int|decimal|bool|json` |
| SystemParameter | Category | string(64) | Sim | Agrupamento opcional |
| SystemParameter | IsSensitive | bool | Não | Default false |
| SystemParameter | LastUpdatedBy | string(64) | Sim | Quem alterou |
| SystemParameter | LastUpdatedAt | DateTime (UTC) | Não | Default now |
| SystemParameter | Reason | string(256) | Sim | Justificativa alteração |
| SystemParameter | Metadata | jsonb/text | Sim | Informações adicionais |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nova entidade `SystemParameter` com invariantes (validação de chave/tipo). | Pode usar Value Objects.
| Application | Interface `ISystemParameterService`, DTOs para consulta/alteração, conversores de tipo. | Injetar onde preciso.
| Infrastructure | Repositório EF Core, configuração da tabela, cache (`IMemoryCache`), invalidação pós-update. | Considerar background refresh opcional.
| API | Endpoints admin `/admin/system-params` com autorização e mascaramento. | Integrar com logging/métricas.

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /admin/system-params | JWT + role admin | REQ-02,REQ-06 | 200 lista mascarada | 401,403 |
| API-02 | GET | /admin/system-params/{key} | JWT + role admin | REQ-02 | 200 valor (mascarado se sensível) | 401,403,404 |
| API-03 | PUT | /admin/system-params/{key} | JWT + role admin | REQ-03,REQ-07 | 200 atualizado | 400,401,403 |
| API-04 | POST | /admin/system-params | JWT + role admin | REQ-03 | 201 criado | 400,401,403,409 |

# 9. Segurança & Performance
- Segurança: mascarar valores sensíveis em listagens/logs; exigir role/claim administrativa; validar input contra injections.
- Performance: cache por chave com TTL configurável (ex.: 60s) + invalidação imediata; fallback para banco.
- Observabilidade: métricas `system_parameter_read_total` e `system_parameter_update_total` por status; logs enriquecidos com `event=SystemParameterChanged`.

# 10. i18n
Não aplicável (mensagens administrativas técnicas). Strings podem permanecer em en-US; considerar localização futura.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | system-params-enabled | Infra | Permite desabilitar módulo em caso de incidente (retornar defaults). | Desligado → fallback para valores padrão |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Criar entidade `SystemParameter` e configuração EF (índices, constraints). | - | REQ-01 |
| TASK-02 | Implementar repositório e serviço com cache (`IMemoryCache`) e conversão de tipos. | TASK-01 | REQ-02,REQ-04 |
| TASK-03 | Implementar invalidação de cache e auditoria pós update. | TASK-02 | REQ-03,REQ-07 |
| TASK-04 | Desenvolver endpoints/admin commands com autorização e mascaramento. | TASK-02 | REQ-06 |
| TASK-05 | Adicionar métricas/logs estruturados para leituras/alterações. | TASK-02 | NFR-03 |
| TASK-06 | Criar testes unitários/integrados cobrindo cache, fallback, tipos e segurança. | TASK-01..03 | NFR-01..05 |
| TASK-07 | Documentar uso (README/ops) com exemplos, defaults e políticas de sensibilidade. | TASK-04 | REQ-05 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Tabela `SystemParameters` criada com índice único em `Key`.
| REQ-02 | Requisição repetida de mesma chave após primeira leitura não consulta banco (cache hit).
| REQ-03 | Atualização via API reflete valor atualizado na próxima leitura (cache invalidado).
| REQ-04 | Conversões de tipos para `int`, `decimal`, `bool`, `json` funcionam e validam entradas inválidas.
| REQ-05 | Uso de método `GetValueOrDefault` retorna fallback quando chave inexistente.
| REQ-06 | Endpoints exigem role adequada; valores sensíveis mascarados em listagem.
| REQ-07 | Logs mostram evento com usuário responsável, motivo e valores antes/depois (mascarado quando preciso).

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-02 | Verifica cache hit e fallback para banco quando cache indisponível. |
| TEST-02 | Unit | REQ-04 | Conversores de tipo validam input e lançam exceções adequadas. |
| TEST-03 | Integration | REQ-03 | Atualização invalida cache e retorna novo valor. |
| TEST-04 | Integration | REQ-06 | Endpoint rejeita usuário sem role admin. |
| TEST-05 | Integration | NFR-03 | Log de alteração contém campos esperados. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Persistência | Usar EF Core com tabela dedicada | Config file/Secrets | Consistência com stack atual |
| DEC-02 | Cache | `IMemoryCache` com invalidação manual | Redis/local cache | Simplicidade inicial |
| DEC-03 | Conversão de tipo | Armazenar como string + Type | Tabela por tipo | Flexibilidade com validação manual |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Quais parâmetros sensíveis precisam mascaramento especial? | owner | Aberto |
| QST-02 | Precisamos de versionamento/histórico completo de alterações? | owner | Aberto |
| QST-03 | Qual estratégia para TTL de cache (fixo ou configurável por chave)? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0007 criando módulo de System Parameters persistido em banco com cache em memória, suporte a múltiplos tipos, invalidação pós update, endpoints administrativos seguros e logging/auditoria básica. Não usar bibliotecas externas. Garantir fallback para defaults e mascaramento de valores sensíveis.

Fim.
