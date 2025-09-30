---
spec:
  id: SPEC-0003
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [reliability, security, observability, performance, delivery_speed, cost]
  tags: [api, error-handling, standards, problem-details]
---

# 1. Objetivo
Padronizar as respostas de erro da Vanq API adotando o formato RFC 7807 (Problem Details), garantindo consistência, melhor experiência para clientes e maior capacidade de depuração.

# 2. Escopo
## 2.1 In
- Introduzir middleware/filtro global que converta exceções e resultados de erro em `ProblemDetails`.
- Mapear erros de autenticação/autorização existentes (`AuthResult`, validações) para objetos Problem Details com códigos personalizados.
- Documentar o contrato de erro no Scalar/OpenAPI e no README.
- Disponibilizar mecanismo para adicionar extensões (`errors`, `traceId`, `timestamp`) às respostas.
- Cobrir cenários com testes automatizados (integração e unidade) garantindo formato esperado.

## 2.2 Out
- Alterações em serviços externos/consumidores (apenas documentar breaking change quando aplicável).
- Implementação de catálogo completo de códigos de erro detalhados além dos já existentes.
- Localização das mensagens de erro (foco atual é formato; i18n virá separadamente se necessário).

## 2.3 Não Fazer
- Expor stack trace completo ou dados sensíveis nas respostas.
- Introduzir dependências externas complexas além do que já existe no ASP.NET Core.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Registrar componente global que converta exceções não tratadas em `ProblemDetails` com `type`, `title`, `status`, `detail`, `instance`. | MUST |
| REQ-02 | Mapear resultados de validação (ex.: `ValidationProblemDetails`) incluindo lista de erros em `extensions.errors`. | MUST |
| REQ-03 | Converter respostas de `AuthResult` e demais resultados customizados para Problem Details, preservando códigos de erro existentes (`errorCode`). | MUST |
| REQ-04 | Incluir `traceId` e `timestamp` (UTC) em `extensions` para todas as respostas Problem Details. | SHOULD |
| REQ-05 | Atualizar documentação pública e interna (Scalar/OpenAPI + README) descrevendo formato e exemplos. | SHOULD |
| REQ-06 | Permitir sobrescrever mensagens/códigos por endpoint quando necessário (ex.: via `IProblemDetailsService`). | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Segurança | Nenhuma resposta Problem Details deve incluir dados sensíveis (hashes, segredos, stack trace). | Revisão de logs sem dados sensíveis |
| NFR-02 | Observabilidade | Registrar log estruturado para cada exceção convertida incluindo `traceId`. | 100% das exceções logadas |
| NFR-03 | Performance | Overhead do middleware de Problem Details deve ser < 3ms p95. | Teste de carga dev |
| NFR-04 | Confiabilidade | Todas as rotas retornam formato consistente (aceitação via testes de integração). | Testes passam |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Código `type` deve apontar para documentação pública (`https://api.vanq.dev/docs/errors/<code>` ou similar). |
| BR-02 | `status` deve refletir fielmente o HTTP retornado (ex.: 400 para validação, 401 para auth). |
| BR-03 | Quando houver `errorCode` interno, incluir em `extensions.errorCode` mantendo compatibilidade. |

# 6. Novas Entidades
Nenhuma nova entidade de domínio é necessária.

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
| Domain | Nenhuma alteração direta. | Revisar enums/códigos de erro se necessário. |
| Application | Ajustar serviços para propagar códigos/mensagens uniformes. | Avaliar interfaces para transporte de metadata. |
| Infrastructure | Criar/adaptar serviços de logging e interceptadores caso usem Problem Details. | |
| API | Configurar `ProblemDetailsOptions`, middleware global e ajustes nos endpoints mínimos. | Provável alteração em `Program.cs` e `Extensions`. |

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | * | * | Conforme endpoint | REQ-01..05 | 2xx payloads existentes | 4xx/5xx no formato Problem Details |

# 9. Segurança & Performance
- Segurança: sanitizar mensagens, evitar leak de informação, monitorar código para não revelar detalhes internos.
- Performance: middleware deve ser leve; reutilizar `ProblemDetailsService` do ASP.NET Core quando possível.
- Observabilidade: correlacionar `traceId` com logs Application Insights/Serilog (se usado). Considerar métrica de contagem de erros por tipo.

# 10. i18n
Não (foco atual em formato). Futuras localizações devem reutilizar estrutura de Problem Details.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | problem-details-enabled | API | Rollout gradual; permite reverter para respostas anteriores em caso de incidente. | Desligado → volta formato atual |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Definir seção `ProblemDetails` em `appsettings` (URLs base, toggles, etc.). | - | REQ-01,REQ-03 |
| TASK-02 | Implementar middleware/filtro que converta exceções e validações para Problem Details. | TASK-01 | REQ-01..04 |
| TASK-03 | Adaptar `AuthEndpoints`/`AuthResultExtensions` para produzir Problem Details. | TASK-02 | REQ-03 |
| TASK-04 | Ajustar logging para incluir `traceId` e metadados. | TASK-02 | NFR-02 |
| TASK-05 | Atualizar documentação (README/Scalar) com seção "Errors" incluindo exemplos. | TASK-03 | REQ-05 |
| TASK-06 | Criar testes de integração cobrindo cenários de validação, auth e exceção não tratada. | TASK-03 | REQ-01..04 |
| TASK-07 | Preparar feature flag `problem-details-enabled` com fallback. | TASK-02 | REQ-06 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Requisição que gera exceção retorna JSON Problem Details com campos básicos preenchidos. |
| REQ-02 | Requisição de validação inválida retorna `extensions.errors` com lista de violações. |
| REQ-03 | Fluxo de auth (ex.: login inválido) retorna Problem Details com `errorCode` e `type` documentados. |
| REQ-04 | Todas as respostas Problem Details incluem `traceId` e `timestamp`. |
| REQ-05 | Documentação apresenta seção de erros com exemplos atualizados. |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-01 | Simula exceção e valida retorno Problem Details. |
| TEST-02 | Integration | REQ-02 | Envia payload inválido e confere `extensions.errors`. |
| TEST-03 | Integration | REQ-03 | Realiza login com credenciais inválidas verificando `errorCode`. |
| TEST-04 | Unit | REQ-04 | Verifica adição de `traceId`/`timestamp` no componente. |
| TEST-05 | Unit | REQ-06 | Garante que feature flag pode desabilitar Problem Details retornando formato anterior. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Basear-se no padrão RFC 7807 | Adotar Problem Details padrão ASP.NET Core | Implementação própria customizada | Menos código customizado e aderência a padrão. |
| DEC-02 | Estrutura `type` | Usar domínio `https://api.vanq.dev/errors/{code}` | URIs relativas ou GUIDs | Facilita documentação centralizada. |
| DEC-03 | Logging | Integrar `traceId` nos logs | Não correlacionar | Melhor troubleshooting. |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Quais códigos de erro legacy precisam manter compatibilidade? | owner | Aberto |
| QST-02 | Há necessidade de i18n imediato para mensagens Problem Details? | owner | Aberto |
| QST-03 | Qual o domínio final para `type` (prod vs staging)? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0003 adicionando suporte global a Problem Details conforme RFC 7807, com middleware, mapeamento de `AuthResult`, extensões (traceId, timestamp, errorCode), documentação e testes. Respeitar feature flag `problem-details-enabled` para rollout controlado.

Fim.
