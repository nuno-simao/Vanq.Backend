---
spec:
  id: SPEC-0005
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [reliability, security, observability, performance, delivery_speed, cost]
  tags: [api, middleware, error-handling, resilience]
---

# 1. Objetivo
Garantir que todas as exceções e falhas de requisições na Vanq API sejam interceptadas por middleware dedicado, retornando respostas padronizadas (alinhadas ao Problem Details) e registrando logs estruturados para observabilidade e troubleshooting.

# 2. Escopo
## 2.1 In
- Criar middleware global `ErrorHandlingMiddleware` responsável por capturar exceções não tratadas.
- Converter exceções em respostas HTTP consistentes (Problem Details, quando habilitado) com códigos apropriados.
- Padronizar logging (nível e estrutura) incluindo `traceId`, `path`, `userId` (quando disponível) e `errorCode`.
- Diferenciar exceções conhecidas (ex.: domínio, validação, autenticação) de erros inesperados.
- Propagar correlação para Application Insights/Serilog (ou provedor atual).
- Atualizar documentação (README/Scalar) com comportamento e payload de erro.

## 2.2 Out
- Implementar catálogos completos de códigos de erro (isso pertence à spec Problem Details).
- Reescrever regras de negócio dos serviços; foco é interceptação no pipeline.
- Migração de logging para ferramenta diferente (só integrar com existente).

## 2.3 Não Fazer
- Expor stack trace ou mensagens internas sensíveis em respostas.
- Tratar erros silenciosamente (tudo deve logar em algum nível).

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Implementar middleware global que capture todas as exceções após os endpoints minimalistas. | MUST |
| REQ-02 | Mapear exceções conhecidas (`DomainException`, `ValidationException`, `AuthenticationException`, etc.) para HTTP status específicos. | MUST |
| REQ-03 | Integrar com formato Problem Details quando `problem-details-enabled` estiver ativo; fallback para JSON simples caso contrário. | MUST |
| REQ-04 | Registrar logs estruturados com nível dependente da severidade (ex.: Warning para validação, Error para falhas 500). | MUST |
| REQ-05 | Incluir correlação (`traceId`, `requestId`, opcionalmente `userId`) nas respostas e logs. | SHOULD |
| REQ-06 | Permitir configuração de mascaramento de mensagens por ambiente (ex.: mensagens detalhadas apenas em dev). | SHOULD |
| REQ-07 | Expor métricas (contador de erros por tipo/status) se observabilidade estiver habilitada. | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Segurança | Mensagens retornadas em produção não devem revelar detalhes internos. | Revisão de payload e logs |
| NFR-02 | Observabilidade | 100% das exceções devem gerar log com `traceId`. | Amostra de logs comprova |
| NFR-03 | Performance | Overhead do middleware < 2ms p95 em requests sem erro. | Testes dev/QA |
| NFR-04 | Confiabilidade | Nenhuma exceção deve vazar sem tratamento (verificado em testes). | Testes automatizados |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Exceções de validação devem retornar HTTP 400 com detalhes dos campos.
| BR-02 | Exceções de autenticação/autorização retornam 401/403 e não devem logar como erro crítico (usar Warning).
| BR-03 | Exceções não mapeadas retornam 500 com mensagem genérica "Unexpected error" e `type` apontando para documentação.

# 6. Novas Entidades
Nenhuma entidade de domínio nova.

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
| Domain | Revisar exceções customizadas para prover `ErrorCode`/`HttpStatus`. | Opcional criar interface `IDomainException`.
| Application | Serviços podem lançar exceções específicas compatíveis com middleware. | Garantir metadados suficientes.
| Infrastructure | Ajustar logging provider (Serilog) para consumir propriedades adicionais. | Avaliar sinks existentes.
| API | Registrar middleware no `Program.cs` antes de endpoints; integrar com Problem Details spec. | Atualizar extensões/helpers.

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | * | * | Conforme endpoint | REQ-01..03 | 2xx iguais | 4xx/5xx uniformizados pelo middleware |

# 9. Segurança & Performance
- Segurança: Sanitizar mensagens; restringir detalhes sensíveis conforme ambiente.
- Performance: Middleware deve ser leve, evitando múltiplas serializações; reutilizar serviços existentes (`ProblemDetailsService`).
- Observabilidade: Emitir métricas/contadores e logs com correlação; considerar integração com Application Insights.

# 10. i18n
Não obrigatório neste escopo (mensagens técnicas); futuras localizações podem aproveitar Problem Details.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | error-middleware-enabled | API | Permite rollback rápido para comportamento anterior em caso de incidente. | Desligado → pipeline atual sem middleware |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Definir seção `ErrorHandling` em `appsettings` (mascaramento, mapping overrides, flag). | - | REQ-03, REQ-06 |
| TASK-02 | Implementar `ErrorHandlingMiddleware` com captura de exceções e resolução de status. | TASK-01 | REQ-01, REQ-02 |
| TASK-03 | Integrar com Problem Details (spec 0003) e fallback JSON. | TASK-02 | REQ-03 |
| TASK-04 | Configurar logging estruturado (Serilog) adicionando `traceId`, `userId`, `errorCode`. | TASK-02 | REQ-04, REQ-05 |
| TASK-05 | Adicionar métricas opcionais (contadores por status). | TASK-02 | REQ-07 |
| TASK-06 | Registrar middleware em `Program.cs` respeitando feature flag. | TASK-02 | REQ-01 |
| TASK-07 | Atualizar documentação (README/Scalar) com exemplos de erros tratados. | TASK-03 | REQ-03 |
| TASK-08 | Criar testes unitários e de integração para exceções mapeadas e genéricas. | TASK-02 | REQ-01..04 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Simulação de exceção genérica resulta em resposta 500 padronizada e log de erro.
| REQ-02 | Exceção de validação retorna 400 com detalhes configurados.
| REQ-03 | Com Problem Details habilitado, resposta inclui `type`, `title`, `status`, `detail`, `instance` e `errorCode` (quando aplicável).
| REQ-04 | Logs demonstram níveis corretos por tipo de exceção.
| REQ-05 | Resposta contém `traceId` correlacionado com log.
| REQ-06 | Ambientes configurados para mascarar detalhes exibem mensagens genéricas.

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-01, REQ-03 | Força exceção no endpoint e valida resposta Problem Details e log.
| TEST-02 | Integration | REQ-02 | Endpoint que lança `ValidationException` retorna 400 com detalhes.
| TEST-03 | Unit | REQ-04 | Middleware registra log correto conforme tipo de exceção.
| TEST-04 | Unit | REQ-05 | Verifica inclusão de `traceId` e `userId` nas propriedades.
| TEST-05 | Integration | REQ-06 | Ambiente simulando produção retorna mensagem genérica.

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Abordagem de tratamento | Usar middleware custom em vez de filtros por endpoint | Usar `UseExceptionHandler` padrão | Maior controle sobre payload/log |
| DEC-02 | Integração Problem Details | Reutilizar spec 0003 para formato | JSON custom | Evita duplicidade e mantém padrão.
| DEC-03 | Logging | Usar Serilog com enrichers de contexto | Logging simples | Mais contexto para troubleshooting.

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Quais exceções de domínio já existem e quais precisam implementar metadados (status/errorCode)? | owner | Aberto |
| QST-02 | Há necessidade de retornar mensagens detalhadas em staging? | owner | Aberto |
| QST-03 | Precisamos integrar com métricas existentes (Prometheus/Application Insights) nesta fase? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0005 adicionando `ErrorHandlingMiddleware` global, mapeando exceções conhecidas para Problem Details, garantindo logging estruturado com correlação e suporte a mascaramento por ambiente. Respeitar feature flag `error-middleware-enabled` e cobrir com testes automatizados.

Fim.
