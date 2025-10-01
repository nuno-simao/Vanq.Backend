---
spec:
  id: SPEC-0009
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [observability, security, reliability, performance, delivery_speed, cost]
  tags: [logging, observability, serilog]
---

# 1. Objetivo
Implementar logging estruturado padrão na Vanq API usando Serilog puro (sem wrappers adicionais), garantindo correlação, enriquecimento de contexto e proteção contra exposição de dados sensíveis.

# 2. Escopo
## 2.1 In
- Configuração de Serilog como logger principal (console + arquivo/opcional sink estruturado).
- Enriquecimento com `TraceId`, `RequestId`, `UserId` (quando houver) e metadados relevantes.
- Estratégias de masking/redação para impedir logs de dados sensíveis (senhas, tokens, PII).
- Middleware/filters para registrar requisições/respostas críticas com payload reduzido.
- Diretrizes e utilitários (`ILoggingContext`, extension methods) para padronizar eventos (`event=AuthLogin`, etc.).
- Documentação e exemplo de configuração por ambiente.

## 2.2 Out
- Integração com plataformas externas (Datadog, Splunk) além de sinks padrão; pode ser considerado depois.
- Reescrita de todo o código legado; foco em pontos críticos inicialmente.
- Logs de auditoria completa (cobrir eventualmente junto com módulo de auditoria).

## 2.3 Não Fazer
- Dependências de frameworks adicionais de logging (ex.: Serilog.AspNetCore) se não forem necessárias.
- Logar payload completo de requisições/respostas sem filtros.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Configurar Serilog como logger principal com enriquecimento de contexto (TraceId, UserId). | MUST |
| REQ-02 | Criar middleware/filtro para logar requisições e respostas relevantes, aplicando redaction. | MUST |
| REQ-03 | Definir política de mascaramento para campos sensíveis (senhas, tokens, emails). | MUST |
| REQ-04 | Disponibilizar helpers (ex.: `LogAuthEvent`) para padronizar eventos com `event` e `status`. | SHOULD |
| REQ-05 | Expor configuração centralizada (`LoggingOptions`) para sinks, níveis por ambiente e toggles. | SHOULD |
| REQ-06 | Garantir logs estruturados em JSON (ex.: console + arquivo). | SHOULD |
| REQ-07 | Documentar guidelines de logging (quando logar, níveis, campos obrigatórios). | SHOULD |
| REQ-08 | Integrar com métricas de falha (incremento de counters) quando apropriado. | MAY |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Segurança | Nenhum log deve conter senha/token/PII em texto claro. | Revisão de logs |
| NFR-02 | Observabilidade | 100% dos erros críticos possuem `TraceId` e `event`. | Auditoria |
| NFR-03 | Performance | Overhead do middleware de request logging < 5ms p95. | Bench dev |
| NFR-04 | Confiabilidade | Logging degrade elegível (fallback para console se sink falhar). | Teste falha |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Todo log deve incluir `event` categorizado (ex.: `UserRegistration`, `UserLoginFailed`).
| BR-02 | Logs de sucesso e falha devem conter `status` (`success|failure|skipped`).
| BR-03 | Campos sensíveis devem ser redigidos (`***`) ou omitidos conforme política.

# 6. Novas Entidades
Nenhuma entidade de domínio; criar classes utilitárias e configurações.

| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | LoggingOptions | Agregar config de sinks, níveis, flags de masking. | Config.

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| LoggingOptions | MinimumLevel | string | Não | `Information` default |
| LoggingOptions | MaskedFields | string[] | Sim | Lista de campos a mascarar (password, token, refreshToken, email, cpf, telefone, phone) |
| LoggingOptions | ConsoleJson | bool | Não | Default true |
| LoggingOptions | FilePath | string | Sim | Habilita sink arquivo |
| LoggingOptions | EnableRequestLogging | bool | Não | controla middleware |
| LoggingOptions | SensitiveValuePlaceholder | string | Não | Default `***` |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nenhuma. | |
| Application | Atualizar serviços para usar helpers de logging estruturado. | Injetar `ILogger<T>`. |
| Infrastructure | Adicionar Serilog no bootstrap, criar `SerilogLoggerFactory` custom. | Cuidar de setup no `Program.cs`.
| API | Registrar middleware de request logging e redaction; atualizar docs. | |

# 8. API (Se aplicável)
Sem novos endpoints; mudanças são cross-cutting.

# 9. Segurança & Performance
- Segurança: aplicar política de masking/redaction; evitar logar request body completo; permitir whitelisting de rotas seguras.
- Performance: monitorar overhead do middleware; configurar `Serilog.Async` opcional para evitar lock.
- Observabilidade: enriquecer com correlation IDs, environment, version; permitir sinks adicionais via config (arquivo, Seq, etc.).

# 10. i18n
Mensagens de log devem preferir inglês padrão; separar `messageTemplate` e usar propriedades para dados dinâmicos.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | structured-logging-enabled | Infra | Permite rollback rápido para logging anterior. | Desligado → usa logger default .NET |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Adicionar pacote Serilog (core + sinks necessários) com configuração mínima. | - | REQ-01 |
| TASK-02 | Criar `LoggingOptions` e ler de `appsettings`. | TASK-01 | REQ-05 |
| TASK-03 | Implementar middleware de request/response logging com redaction. | TASK-01 | REQ-02,REQ-03 |
| TASK-04 | Criar helpers/extension methods para logs de domínio (`LogAuthEvent`, etc.). | TASK-01 | REQ-04 |
| TASK-05 | Configurar sinks (console JSON, arquivo) e fallback. | TASK-01 | REQ-06 |
| TASK-06 | Atualizar serviços críticos (AuthService, RefreshTokenService) para usar logs estruturados. | TASK-04 | BR-01..
| TASK-07 | Documentar guidelines e exemplos no README/docs. | TASK-04 | REQ-07 |
| TASK-08 | Adicionar testes/unit/integration validando masking e presença de campos (TraceId). | TASK-03 | NFR-01, NFR-02 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Serilog configurado como logger principal; aplicação inicia com logs estruturados JSON.
| REQ-02 | Middleware registra requests relevantes com campo `event=RequestCompleted` e sem dados sensíveis.
| REQ-03 | Logs de senhas/tokens mostram `***` (testado automatizadamente).
| REQ-04 | Helper `LogAuthEvent` gera log com `event=AuthLogin`, `status=success|failure` e `userId` (quando disponível).
| REQ-05 | `LoggingOptions` permite ajustar níveis/sinks sem recompilar.

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-03 | Verifica redaction de campos sensíveis.
| TEST-02 | Integration | REQ-02 | Middleware gera log com `TraceId` e `event`.
| TEST-03 | Unit | REQ-04 | Helper gera log com propriedades esperadas.
| TEST-04 | Integration | NFR-04 | Simula falha no sink principal e confirma fallback.
| TEST-05 | Unit | REQ-05 | `LoggingOptions` carrega valores configurados corretamente.

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Logger | Usar Serilog puro (Serilog.Core + sinks) | Microsoft.Extensions.Logging padrão | Maior controle de formato.
| DEC-02 | Formato | JSON estruturado com message templates | Texto simples | Facilita ingestão por observability stack.
| DEC-03 | Redaction | Lista configurável de campos + regex para padrões | Redaction manual em cada log | Centraliza política.
| DEC-04 | Sink Produção | Usar apenas File sink em produção | Seq/Elastic/Cloud providers | Simplicidade, menor custo, logs locais auditáveis.
| DEC-05 | OpenTelemetry | Não integrar com OTel nesta spec | Correlação com traces distribuídos | Reduz complexidade inicial; pode ser adicionado futuramente.

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status | Resposta |
|----|----------|-------------|--------|----------|
| QST-01 | Quais sinks serão usados em produção (Seq, Elastic, File)? | owner | Resolvido | File apenas |
| QST-02 | Precisamos correlacionar logs com métricas/traces (OpenTelemetry)? | owner | Resolvido | Não (fora do escopo) |
| QST-03 | Campos sensíveis adicionais (CPF, telefone) devem ser mascarados por padrão? | owner | Resolvido | Sim, incluir na lista de masked fields |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0009 configurando Serilog puro como logger estruturado, adicionando middleware de request logging com redaction, helpers para eventos, opções configuráveis e testes garantindo ausência de dados sensíveis nos logs.

Fim.
