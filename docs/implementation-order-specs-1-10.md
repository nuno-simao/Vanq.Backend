# Ordem Sugerida de Implementação - SPECs 1 a 10

**Data:** 2025-10-01  
**Contexto:** Análise de dependências e prioridades das especificações 1-5 e 7-10  
**Objetivo:** Sugerir ordem lógica de implementação considerando dependências técnicas e valor de negócio

---

## 📊 Análise de Dependências

### Mapa de Relacionamentos

```
SPEC-0009 (Logging)  ─────┐
                          │
SPEC-0003 (Problem Det.)  ├─→ SPEC-0005 (Error Middleware)
                          │         │
                          │         ├─→ SPEC-0008 (Rate Limiting)
                          │         │
                          └─────────┴─→ SPEC-0010 (Metrics)

SPEC-0004 (Health Checks) ──→ (standalone, baixa dependência)

SPEC-0002 (CORS) ──→ (standalone, infraestrutura básica)

SPEC-0001 (User Registration) ──→ (funcional, já implementado base)

SPEC-0007 (System Params) ──→ (funcional, média independência)
```

### Dependências Identificadas

| SPEC | Depende de | Tipo de Dependência | Observações |
|------|------------|---------------------|-------------|
| SPEC-0001 | Nenhuma | - | Funcionalidade já implementada parcialmente |
| SPEC-0002 | Nenhuma | - | Configuração de infraestrutura independente |
| SPEC-0003 | Nenhuma | - | Pode ser base para outras specs |
| SPEC-0004 | Nenhuma | - | Standalone, mas beneficia de logging |
| SPEC-0005 | SPEC-0003 | Forte | REQ-03 integra com Problem Details |
| SPEC-0005 | SPEC-0009 | Moderada | REQ-04 requer logging estruturado |
| SPEC-0007 | Nenhuma | - | Funcionalidade independente |
| SPEC-0008 | SPEC-0005 | Moderada | Retorna Problem Details (REQ-04) |
| SPEC-0008 | SPEC-0009 | Moderada | REQ-05 requer logging estruturado |
| SPEC-0009 | Nenhuma | - | Base de observabilidade |
| SPEC-0010 | SPEC-0009 | Fraca | Beneficia de contexto de logging |
| SPEC-0010 | SPEC-0005 | Fraca | REQ-07 (métricas de erros) |

---

## 🎯 Ordem de Implementação Sugerida

### **FASE 1: Fundação de Observabilidade** (Semanas 1-2)

#### **1. SPEC-0009 - Structured Logging** 🔴 PRIORIDADE ALTA
**Justificativa:** Base para todas as outras specs de observabilidade e troubleshooting.

**Impacto:**
- ✅ **Habilita:** SPEC-0003, SPEC-0005, SPEC-0008, SPEC-0010
- ✅ **Valor de Negócio:** Alto - essencial para operação e debugging em produção
- ✅ **Risco:** Baixo - não afeta funcionalidades de negócio existentes
- ✅ **Complexidade:** Média - configuração Serilog + enriquecimento

**Ordem de Tarefas:**
1. Configurar Serilog como logger principal (REQ-01)
2. Criar middleware de enriquecimento (TraceId, UserId) (REQ-01)
3. Implementar política de mascaramento (REQ-03)
4. Criar helpers de logging padronizado (REQ-04)
5. Configurar sinks e níveis por ambiente (REQ-05, REQ-06)
6. Documentar guidelines (REQ-07)

**Critérios de Aceite:**
- [ ] Logs em formato JSON estruturado
- [ ] TraceId presente em 100% dos logs
- [ ] Dados sensíveis mascarados
- [ ] Guidelines documentadas

---

#### **2. SPEC-0003 - Problem Details (RFC 7807)** 🔴 PRIORIDADE ALTA
**Justificativa:** Padroniza formato de erros antes de implementar middleware e rate limiting.

**Impacto:**
- ✅ **Habilita:** SPEC-0005 (forte dependência)
- ✅ **Melhora:** SPEC-0008 (retornos consistentes)
- ✅ **Valor de Negócio:** Alto - melhora experiência de integração para clientes
- ✅ **Risco:** Médio - breaking change potencial em APIs existentes
- ✅ **Complexidade:** Baixa - usa recursos nativos ASP.NET Core

**Ordem de Tarefas:**
1. Configurar `ProblemDetailsOptions` global (REQ-01)
2. Mapear `ValidationProblemDetails` (REQ-02)
3. Converter `AuthResult` para Problem Details (REQ-03)
4. Adicionar `traceId` e `timestamp` em extensions (REQ-04)
5. Atualizar documentação Scalar/OpenAPI (REQ-05)
6. Criar testes de integração (NFR-04)

**Critérios de Aceite:**
- [ ] 100% das rotas retornam Problem Details em erros
- [ ] TraceId presente em todas as respostas de erro
- [ ] Documentação Scalar atualizada
- [ ] Testes de integração passando

---

### **FASE 2: Tratamento de Erros e Infraestrutura** (Semanas 3-4)

#### **3. SPEC-0005 - Error Handling Middleware** 🔴 PRIORIDADE ALTA
**Justificativa:** Centraliza tratamento de exceções com formato já padronizado (Problem Details).

**Depende de:**
- ✅ SPEC-0009 (logging estruturado) - REQ-04
- ✅ SPEC-0003 (Problem Details) - REQ-03

**Impacto:**
- ✅ **Habilita:** SPEC-0008, SPEC-0010 (métricas de erro)
- ✅ **Valor de Negócio:** Alto - confiabilidade e resiliência
- ✅ **Risco:** Médio - afeta todas as requisições
- ✅ **Complexidade:** Média - mapeamento de exceções e integração

**Ordem de Tarefas:**
1. Criar `ErrorHandlingMiddleware` global (REQ-01)
2. Mapear exceções conhecidas para HTTP status (REQ-02)
3. Integrar com Problem Details (REQ-03)
4. Implementar logging estruturado com severidade (REQ-04)
5. Adicionar correlação (traceId, requestId, userId) (REQ-05)
6. Configurar mascaramento por ambiente (REQ-06)
7. Integrar métricas de erros (REQ-07, opcional)
8. Criar testes automatizados (NFR-04)

**Critérios de Aceite:**
- [ ] 100% exceções não tratadas interceptadas
- [ ] Logs estruturados com traceId
- [ ] Problem Details retornado em erros
- [ ] Nenhuma informação sensível em produção
- [ ] Feature flag `error-middleware-enabled` funcional

---

#### **4. SPEC-0002 - CORS Support** 🟡 PRIORIDADE MÉDIA
**Justificativa:** Configuração de infraestrutura necessária para clientes web, sem dependências.

**Impacto:**
- ✅ **Habilita:** Integrações frontend
- ✅ **Valor de Negócio:** Alto - essencial para aplicações web
- ✅ **Risco:** Baixo - configuração isolada
- ✅ **Complexidade:** Baixa - configuração nativa ASP.NET Core

**Ordem de Tarefas:**
1. Criar política CORS nomeada `vanq-default-cors` (REQ-01)
2. Configurar origens via `appsettings.json` (REQ-01)
3. Aplicar política globalmente no pipeline (REQ-02)
4. Configurar métodos e cabeçalhos permitidos (REQ-03)
5. Implementar modo desenvolvimento permissivo (REQ-04)
6. Documentar configuração (REQ-05)
7. Criar testes de smoke (validar headers)

**Critérios de Aceite:**
- [ ] Política CORS aplicada globalmente
- [ ] Origens configuráveis por ambiente
- [ ] Dev mode permissivo funcionando
- [ ] Produção restrita a origens autorizadas
- [ ] Documentação completa

---

#### **5. SPEC-0004 - Health Checks** 🟡 PRIORIDADE MÉDIA
**Justificativa:** Monitoramento de disponibilidade essencial para operações, sem dependências fortes.

**Impacto:**
- ✅ **Habilita:** Pipelines de CI/CD, monitoramento
- ✅ **Valor de Negócio:** Médio/Alto - operações e confiabilidade
- ✅ **Risco:** Baixo - endpoints isolados
- ✅ **Complexidade:** Baixa - usa biblioteca nativa

**Ordem de Tarefas:**
1. Adicionar pacote `Microsoft.Extensions.Diagnostics.HealthChecks` (REQ-01)
2. Registrar health check de PostgreSQL (REQ-01)
3. Criar endpoint `/health` (liveness) (REQ-02)
4. Criar endpoint `/health/ready` (readiness) (REQ-03)
5. Validar variáveis de ambiente críticas (REQ-04)
6. Implementar payload JSON com detalhes (REQ-05)
7. Configurar timeouts apropriados (REQ-06)
8. Documentar uso e interpretação (README)

**Critérios de Aceite:**
- [ ] `/health` retorna 200 quando app ativo
- [ ] `/health/ready` valida DB e env vars
- [ ] Payload JSON com status, duração e detalhes
- [ ] Timeout configurável (default 3s)
- [ ] Logs em caso de unhealthy
- [ ] Feature flag `health-checks-enabled` funcional

---

### **FASE 3: Segurança e Métricas** (Semanas 5-6)

#### **6. SPEC-0008 - Rate Limiting** 🔴 PRIORIDADE ALTA
**Justificativa:** Proteção contra abuso e controle de recursos, depende de error handling.

**Depende de:**
- ✅ SPEC-0005 (Error Middleware) - para retornar Problem Details em 429
- ✅ SPEC-0009 (Logging) - REQ-05 (logs de bloqueios)

**Impacto:**
- ✅ **Valor de Negócio:** Alto - segurança e disponibilidade
- ✅ **Risco:** Médio - pode afetar usuários legítimos se mal configurado
- ✅ **Complexidade:** Média - configuração de políticas e identificação

**Ordem de Tarefas:**
1. Configurar `AddRateLimiter` com políticas nomeadas (REQ-01)
2. Definir política global e específica `/auth/*` (REQ-02)
3. Implementar identificação (API Key, userId, IP) (REQ-03)
4. Configurar resposta 429 com Problem Details e `Retry-After` (REQ-04)
5. Implementar métricas e logging de bloqueios (REQ-05)
6. Permitir override por ambiente (REQ-06)
7. Criar endpoint de consulta (REQ-07, opcional)
8. Documentar configuração e testes

**Critérios de Aceite:**
- [ ] Políticas de rate limiting ativas
- [ ] 429 retornado com Problem Details e Retry-After
- [ ] Identificação funcional (API Key → userId → IP)
- [ ] Métricas de bloqueios disponíveis
- [ ] Configurável por ambiente
- [ ] Feature flag `rate-limiting-enabled` funcional

---

#### **7. SPEC-0010 - Metrics (Telemetry)** 🔴 PRIORIDADE ALTA
**Justificativa:** Observabilidade complementar ao logging, essencial para monitoramento.

**Depende de:**
- ✅ SPEC-0009 (Logging) - contexto de correlação (fraca)
- ✅ SPEC-0005 (Error Middleware) - métricas de erros (fraca)

**Impacto:**
- ✅ **Valor de Negócio:** Alto - visibilidade de operação e performance
- ✅ **Risco:** Baixo - não afeta funcionalidades
- ✅ **Complexidade:** Média - instrumentação e exposição

**Ordem de Tarefas:**
1. Configurar `IMeterFactory` e registrar meters (REQ-01)
2. Criar contador `auth_user_registration_total` (REQ-02)
3. Instrumentar latência (histogram) de endpoints críticos (REQ-03)
4. Expor endpoint `/metrics` (Prometheus/OpenMetrics) (REQ-04)
5. Documentar métricas disponíveis (REQ-05)
6. Criar abstração `IMetricsService` (REQ-06)
7. Permitir toggle de métricas detalhadas (REQ-07)
8. Configurar feature flag e autenticação opcional

**Critérios de Aceite:**
- [ ] Meters registrados para Auth, Infra, API
- [ ] Contador de registros com label `status`
- [ ] Histograma de latência em endpoints críticos
- [ ] `/metrics` expondo formato Prometheus
- [ ] Documentação completa de métricas
- [ ] Feature flag `metrics-enabled` funcional

---

### **FASE 4: Funcionalidades de Negócio** (Semanas 7-8)

#### **8. SPEC-0007 - System Parameters** 🟡 PRIORIDADE MÉDIA
**Justificativa:** Configuração dinâmica útil mas sem dependências críticas de outras specs.

**Impacto:**
- ✅ **Valor de Negócio:** Médio - flexibilidade operacional
- ✅ **Risco:** Baixo - funcionalidade isolada
- ✅ **Complexidade:** Média - entidade, cache, invalidação

**Ordem de Tarefas:**
1. Criar entidade `SystemParameter` e migration (REQ-01)
2. Implementar `ISystemParameterService` com cache (REQ-02)
3. Criar operações CRUD com invalidação de cache (REQ-03)
4. Suportar tipos primários (string, int, bool, json) (REQ-04)
5. Implementar fallback para defaults (REQ-05)
6. Criar endpoint/admin command protegido (REQ-06)
7. Adicionar logging e auditoria (REQ-07)
8. Implementar namespace/categoria opcional (REQ-08)
9. Documentar uso e exemplos

**Critérios de Aceite:**
- [ ] Tabela `SystemParameters` criada
- [ ] Cache em memória com invalidação automática
- [ ] CRUD funcional com proteção RBAC
- [ ] Suporte a tipos: string, int, bool, json
- [ ] Logging de alterações
- [ ] Feature flag `system-params-enabled` funcional

---

#### **9. SPEC-0001 - User Registration** 🟢 PRIORIDADE BAIXA
**Justificativa:** Funcionalidade já implementada; formalizar retroativamente conforme spec.

**Impacto:**
- ✅ **Valor de Negócio:** Baixo - já existe implementação base
- ✅ **Risco:** Baixo - ajustes incrementais
- ✅ **Complexidade:** Baixa - refinamentos e testes

**Ordem de Tarefas:**
1. Revisar implementação existente vs SPEC-0001
2. Adicionar validações faltantes (REQ-01, REQ-02)
3. Garantir geração de refresh token (REQ-03)
4. Registrar timestamps (REQ-04)
5. Implementar i18n de mensagens de validação (REQ-05)
6. Adicionar métrica de sucesso de registro (REQ-06)
7. Criar testes de conformidade
8. Documentar feature flag `user-registration-enabled`

**Critérios de Aceite:**
- [ ] Validação de email único
- [ ] Senha com requisitos mínimos
- [ ] Tokens (access + refresh) retornados
- [ ] Mensagens traduzíveis (pt-BR, en-US)
- [ ] Métrica de registro disponível
- [ ] Feature flag funcional

---

## 📊 Resumo da Ordem Recomendada

| # | SPEC | Nome | Prioridade | Semanas | Dependências |
|---|------|------|------------|---------|--------------|
| 1 | SPEC-0009 | Structured Logging | 🔴 Alta | 1-2 | Nenhuma |
| 2 | SPEC-0003 | Problem Details | 🔴 Alta | 1-2 | SPEC-0009 (fraca) |
| 3 | SPEC-0005 | Error Handling Middleware | 🔴 Alta | 3-4 | SPEC-0009, SPEC-0003 |
| 4 | SPEC-0002 | CORS Support | 🟡 Média | 3-4 | Nenhuma |
| 5 | SPEC-0004 | Health Checks | 🟡 Média | 3-4 | SPEC-0009 (fraca) |
| 6 | SPEC-0008 | Rate Limiting | 🔴 Alta | 5-6 | SPEC-0005, SPEC-0009 |
| 7 | SPEC-0010 | Metrics (Telemetry) | 🔴 Alta | 5-6 | SPEC-0009, SPEC-0005 (fraca) |
| 8 | SPEC-0007 | System Parameters | 🟡 Média | 7-8 | Nenhuma |
| 9 | SPEC-0001 | User Registration | 🟢 Baixa | 7-8 | Nenhuma (já implementado) |

---

## 🎯 Estratégia de Implementação

### Paralelização Possível

**Fase 1 (Semanas 1-2):**
- SPEC-0009 → SPEC-0003 (sequencial)

**Fase 2 (Semanas 3-4):**
- SPEC-0005 (depende de fase 1)
- SPEC-0002 (paralelo, independente) 👥
- SPEC-0004 (paralelo, independente) 👥

**Fase 3 (Semanas 5-6):**
- SPEC-0008 (depende de SPEC-0005)
- SPEC-0010 (paralelo com SPEC-0008) 👥

**Fase 4 (Semanas 7-8):**
- SPEC-0007 (paralelo) 👥
- SPEC-0001 (paralelo) 👥

### Benefícios da Ordem Proposta

1. **✅ Fundação Sólida:** Observabilidade primeiro permite troubleshooting desde o início
2. **✅ Dependências Respeitadas:** Nenhuma spec bloqueada esperando outra
3. **✅ Valor Incremental:** Cada fase entrega valor independente
4. **✅ Risco Controlado:** Specs de infraestrutura antes de funcionalidades críticas
5. **✅ Paralelização:** Permite trabalho simultâneo em specs independentes
6. **✅ Facilita Rollback:** Feature flags permitem desabilitar funcionalidades problemáticas

---

## 🔄 Alternativas Consideradas

### Opção B: Priorizar Funcionalidades de Negócio
**Ordem:** SPEC-0001 → SPEC-0007 → SPEC-0009 → SPEC-0003 → ...

**Rejeição:** Dificulta debugging e troubleshooting durante desenvolvimento; observabilidade deve vir primeiro.

---

### Opção C: Implementar por Complexidade
**Ordem:** SPEC-0002 → SPEC-0004 → SPEC-0003 → SPEC-0009 → ...

**Rejeição:** Não respeita dependências lógicas; specs simples não necessariamente entregam mais valor.

---

## 📝 Notas de Implementação

### Considerações Gerais

1. **Feature Flags:** Todas as specs devem usar feature flags para ativação gradual
2. **Documentação:** Atualizar README e Scalar após cada spec implementada
3. **Testes:** Cobertura mínima de 80% para cada spec antes de considerar concluída
4. **Code Review:** Todas as PRs devem referenciar a SPEC correspondente
5. **Changelog:** Manter CHANGELOG.md atualizado com mudanças de cada spec

### Métricas de Sucesso

- **Velocidade:** ~2 specs por sprint (2 semanas)
- **Qualidade:** Zero regressões após implementação
- **Observabilidade:** 100% das specs instrumentadas (logs + métricas)
- **Documentação:** 100% das specs com guias de uso

---

## 📚 Referências

- **SPECs Analisadas:** SPEC-0001, SPEC-0002, SPEC-0003, SPEC-0004, SPEC-0005, SPEC-0007, SPEC-0008, SPEC-0009, SPEC-0010
- **SPECs Implementadas:** SPEC-0006 (Feature Flags), SPEC-0011 (RBAC)
- **Dependências Mapeadas:** Via análise de REQs e seções de cada spec
- **Template Base:** templates/spec.md

---

**Preparado por:** GitHub Copilot  
**Data:** 2025-10-01  
**Versão:** 1.0  
**Status:** Recomendação para discussão e aprovação
