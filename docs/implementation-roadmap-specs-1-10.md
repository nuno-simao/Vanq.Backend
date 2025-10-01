# 🗓️ Roadmap de Implementação - SPECs 1-10

**Período Planejado:** 8-10 semanas  
**Data de Início Sugerida:** A definir  
**Última Atualização:** 2025-10-01

---

## 📅 Timeline Visual

```
Semana │ Sprint │ Specs                    │ Entregável
───────┼────────┼──────────────────────────┼─────────────────────────────────
  1-2  │   1    │ SPEC-0009 + SPEC-0003    │ Observabilidade + Erro Padrão
       │        │ (Logging + ProblemDet)   │
───────┼────────┼──────────────────────────┼─────────────────────────────────
  3-4  │   2    │ SPEC-0005                │ Tratamento Global de Erros
       │        │ SPEC-0002 (paralelo)     │ + CORS
       │        │ SPEC-0004 (paralelo)     │ + Health Checks
───────┼────────┼──────────────────────────┼─────────────────────────────────
  5-6  │   3    │ SPEC-0008                │ Rate Limiting
       │        │ SPEC-0010 (paralelo)     │ + Métricas/Telemetria
───────┼────────┼──────────────────────────┼─────────────────────────────────
  7-8  │   4    │ SPEC-0007 (paralelo)     │ System Parameters
       │        │ SPEC-0001 (paralelo)     │ + User Registration (formal)
───────┴────────┴──────────────────────────┴─────────────────────────────────
```

---

## 🎯 Sprint Planning

### **Sprint 1 (Semanas 1-2): Fundação de Observabilidade**

#### Objetivo
Estabelecer base sólida de observabilidade e padronização de erros.

#### SPECs
- **SPEC-0009:** Structured Logging (Serilog)
- **SPEC-0003:** Problem Details (RFC 7807)

#### Story Points
- SPEC-0009: 13 pontos (complexidade média-alta)
- SPEC-0003: 8 pontos (complexidade média)
- **Total:** 21 pontos

#### Critérios de Aceite do Sprint
- [ ] Logs em JSON estruturado funcionando
- [ ] TraceId presente em 100% dos logs
- [ ] Dados sensíveis mascarados
- [ ] Problem Details retornado em erros de validação e autenticação
- [ ] Documentação atualizada (README + Scalar)
- [ ] Feature flags `structured-logging-enabled` e `problem-details-enabled` funcionais
- [ ] Testes passando (cobertura ≥80%)

#### Riscos
- ⚠️ **Médio:** Mudança de provedor de logging pode impactar performance inicial
- ⚠️ **Baixo:** Problem Details pode quebrar integrações existentes

#### Mitigação
- Benchmarks de performance antes e depois
- Feature flags para rollback rápido
- Comunicar breaking change (se aplicável)

---

### **Sprint 2 (Semanas 3-4): Tratamento de Erros e Infraestrutura**

#### Objetivo
Centralizar tratamento de exceções e habilitar integrações web.

#### SPECs
- **SPEC-0005:** Error Handling Middleware
- **SPEC-0002:** CORS Support (paralelo)
- **SPEC-0004:** Health Checks (paralelo)

#### Story Points
- SPEC-0005: 13 pontos (complexidade média-alta)
- SPEC-0002: 5 pontos (complexidade baixa)
- SPEC-0004: 8 pontos (complexidade média)
- **Total:** 26 pontos

#### Critérios de Aceite do Sprint
- [ ] Middleware captura 100% exceções não tratadas
- [ ] Problem Details retornado em erros do middleware
- [ ] Logs estruturados de todas as exceções
- [ ] CORS configurado e testado com origens de dev/staging
- [ ] Health checks `/health` e `/health/ready` funcionais
- [ ] Validação de DB e env vars implementada
- [ ] Feature flags ativas: `error-middleware-enabled`, `cors-relaxed`, `health-checks-enabled`
- [ ] Documentação completa

#### Paralelização
```
Dev A: SPEC-0005 (Error Middleware) ───────────────┐
                                                    ├─> Merge Sprint 2
Dev B: SPEC-0002 (CORS) → SPEC-0004 (Health) ─────┘
```

#### Riscos
- ⚠️ **Alto:** Middleware de erro pode adicionar latência inaceitável
- ⚠️ **Médio:** CORS mal configurado pode bloquear clientes legítimos

#### Mitigação
- Benchmarks com threshold < 2ms p95
- Configuração permissiva em dev, restrita em prod
- Testes de integração end-to-end

---

### **Sprint 3 (Semanas 5-6): Segurança e Observabilidade Avançada**

#### Objetivo
Proteger recursos contra abuso e instrumentar métricas de negócio.

#### SPECs
- **SPEC-0008:** Rate Limiting
- **SPEC-0010:** Metrics (Telemetry) (paralelo)

#### Story Points
- SPEC-0008: 13 pontos (complexidade média-alta)
- SPEC-0010: 13 pontos (complexidade média-alta)
- **Total:** 26 pontos

#### Critérios de Aceite do Sprint
- [ ] Rate limiting ativo em `/auth/*` com limites configuráveis
- [ ] 429 retornado com Problem Details e `Retry-After`
- [ ] Identificação de consumidor (API Key → userId → IP)
- [ ] Métricas instrumentadas: registros, logins, latência, erros
- [ ] Endpoint `/metrics` expondo Prometheus format
- [ ] Logs de bloqueios e métricas de rate limit
- [ ] Feature flags ativas: `rate-limiting-enabled`, `metrics-enabled`
- [ ] Dashboard básico sugerido (Grafana/AppInsights)

#### Paralelização
```
Dev A: SPEC-0008 (Rate Limiting) ──────────────────┐
                                                    ├─> Merge Sprint 3
Dev B: SPEC-0010 (Metrics/Telemetry) ──────────────┘
```

#### Riscos
- ⚠️ **Alto:** Rate limiting muito restritivo pode frustrar usuários legítimos
- ⚠️ **Médio:** Overhead de coleta de métricas pode impactar performance

#### Mitigação
- Limites conservadores inicialmente (100 req/min)
- Monitorar false positives primeiras 2 semanas
- Métricas essenciais primeiro, detalhadas via flag
- Benchmarks com target overhead < 5%

---

### **Sprint 4 (Semanas 7-8): Funcionalidades de Negócio**

#### Objetivo
Adicionar configuração dinâmica e formalizar registro de usuários.

#### SPECs
- **SPEC-0007:** System Parameters
- **SPEC-0001:** User Registration (formal)

#### Story Points
- SPEC-0007: 13 pontos (complexidade média-alta)
- SPEC-0001: 5 pontos (complexidade baixa - já implementado)
- **Total:** 18 pontos

#### Critérios de Aceite do Sprint
- [ ] Entidade `SystemParameter` criada com migration
- [ ] Cache em memória com invalidação automática
- [ ] CRUD protegido por RBAC
- [ ] Suporte a tipos: string, int, bool, json
- [ ] Registro de usuário validado conforme SPEC-0001
- [ ] Mensagens de validação traduzíveis (pt-BR, en-US)
- [ ] Métrica `auth_user_registration_total` funcionando
- [ ] Feature flags ativas: `system-params-enabled`, `user-registration-enabled`
- [ ] Documentação de uso

#### Paralelização
```
Dev A: SPEC-0007 (System Parameters) ──────────────┐
                                                    ├─> Merge Sprint 4
Dev B: SPEC-0001 (User Registration formal) ───────┘
```

#### Riscos
- ⚠️ **Baixo:** Cache de parâmetros pode não invalidar corretamente
- ⚠️ **Baixo:** Alterações em SPEC-0001 podem quebrar clientes existentes

#### Mitigação
- Testes de invalidação de cache
- Testes de concorrência
- Backward compatibility mantida
- Feature flag para rollback

---

## 📊 Quadro Kanban

```
┌─────────────┬─────────────┬─────────────┬─────────────┐
│   BACKLOG   │  TO DO      │  IN PROGRESS│    DONE     │
├─────────────┼─────────────┼─────────────┼─────────────┤
│             │             │             │ SPEC-0006   │
│ SPEC-0001   │ SPEC-0009   │             │ (FeatureFlg)│
│ SPEC-0002   │ SPEC-0003   │             │             │
│ SPEC-0003   │             │             │ SPEC-0011   │
│ SPEC-0004   │             │             │ (RBAC)      │
│ SPEC-0005   │             │             │             │
│ SPEC-0007   │             │             │             │
│ SPEC-0008   │             │             │             │
│ SPEC-0009   │             │             │             │
│ SPEC-0010   │             │             │             │
└─────────────┴─────────────┴─────────────┴─────────────┘
```

**Status Atual:** Pré-planejamento (aguardando início)

---

## 🎯 Definition of Ready (DoR)

Antes de mover uma SPEC para **TO DO**:
- [ ] SPEC revisada pelo time
- [ ] Dependências identificadas e resolvidas
- [ ] Critérios de aceite claros
- [ ] Story points estimados
- [ ] Feature flag planejada
- [ ] Impactos arquiteturais entendidos

---

## ✅ Definition of Done (DoD)

Antes de marcar uma SPEC como **DONE**:
- [ ] Todos os REQs MUST implementados
- [ ] Testes unitários escritos e passando
- [ ] Testes de integração cobrindo fluxos principais
- [ ] Cobertura de código ≥ 80%
- [ ] Feature flag testada (on/off)
- [ ] Documentação atualizada (README, Scalar, `/docs`)
- [ ] Code review aprovado por 2+ desenvolvedores
- [ ] PR mergeada para `main`
- [ ] Deploy em ambiente de staging validado
- [ ] Sem warnings de compilação
- [ ] Benchmarks de performance atendidos (se aplicável)

---

## 🔄 Rituais Ágeis Sugeridos

### Daily Standup (15min)
- O que fiz ontem relacionado às SPECs?
- O que farei hoje?
- Algum bloqueio ou impedimento?
- Alguma dependência não mapeada descoberta?

### Sprint Planning (2h no início de cada sprint)
- Review do backlog e priorização
- Estimativa de story points
- Atribuição de SPECs aos desenvolvedores
- Identificação de riscos e mitigações

### Sprint Review (1h no fim de cada sprint)
- Demo das SPECs implementadas
- Validação dos critérios de aceite
- Feedback do product owner
- Decisão de aceitar ou retrabalhar

### Sprint Retrospective (1h no fim de cada sprint)
- O que funcionou bem?
- O que pode melhorar?
- Action items para próximo sprint
- Revisão de velocity e ajustes

---

## 📈 Métricas de Acompanhamento

### Velocity
| Sprint | Story Points Planejados | Story Points Concluídos | % Completude |
|--------|-------------------------|-------------------------|--------------|
| 1      | 21                      | -                       | -            |
| 2      | 26                      | -                       | -            |
| 3      | 26                      | -                       | -            |
| 4      | 18                      | -                       | -            |
| **Total** | **91**              | **-**                   | **-**        |

### Burndown (Story Points)
```
91│●
  │ ╲
80│  ╲
  │   ●
70│    ╲
  │     ╲
60│      ●
  │       ╲
50│        ╲
  │         ●
40│          ╲
  │           ╲
30│            ●
  │             ╲
20│              ●
  │               ╲
10│                ●
  │                 ╲
 0│                  ●
  └─────────────────────
   S1  S2  S3  S4  Fim
```

### Qualidade
| Métrica | Target | Sprint 1 | Sprint 2 | Sprint 3 | Sprint 4 |
|---------|--------|----------|----------|----------|----------|
| Cobertura de Testes | ≥80% | - | - | - | - |
| Bugs Encontrados | 0 | - | - | - | - |
| Regressões | 0 | - | - | - | - |
| Code Review Aprovações | 100% | - | - | - | - |

---

## 🚦 Status Report Template

```markdown
## Status Report - Sprint X (Semanas Y-Z)

**Data:** YYYY-MM-DD
**Sprint:** X
**Status Geral:** 🟢 On Track / 🟡 At Risk / 🔴 Blocked

### SPECs em Progresso
- [ ] SPEC-00XX: Nome (Dev: @username) - XX% completo

### SPECs Concluídas
- [x] SPEC-00XX: Nome (Merged: PR#123)

### Impedimentos
- Nenhum / [Descrição do bloqueio]

### Métricas
- Story Points Concluídos: X/Y
- Cobertura de Testes: XX%
- PRs Abertos: X

### Próximos Passos
1. [Ação 1]
2. [Ação 2]
```

---

## 🎉 Milestones

| Milestone | SPECs Incluídas | Data Target | Status |
|-----------|-----------------|-------------|--------|
| **M1: Observabilidade Base** | SPEC-0009, SPEC-0003 | Semana 2 | 🔲 Não Iniciado |
| **M2: Infraestrutura Resiliente** | SPEC-0005, SPEC-0002, SPEC-0004 | Semana 4 | 🔲 Não Iniciado |
| **M3: Segurança e Telemetria** | SPEC-0008, SPEC-0010 | Semana 6 | 🔲 Não Iniciado |
| **M4: Funcionalidades Completas** | SPEC-0007, SPEC-0001 | Semana 8 | 🔲 Não Iniciado |

---

## 📞 Contatos do Time

| Nome | Papel | SPECs Atribuídas | Email / Slack |
|------|-------|------------------|---------------|
| TBD | Tech Lead | Todas (review) | - |
| TBD | Dev Backend | SPEC-0009, 0005, 0008 | - |
| TBD | Dev Backend | SPEC-0003, 0002, 0004, 0010 | - |
| TBD | QA Engineer | Testes de todas | - |

---

## 📚 Recursos e Links

- **Repositório:** https://github.com/nuno-simao/Vanq.Backend
- **Board Kanban:** [URL do Jira/Azure DevOps]
- **Documentação:** `/docs`
- **CI/CD:** [URL do pipeline]
- **Ambiente Staging:** [URL]
- **Métricas (Grafana):** [URL quando disponível]

---

**Roadmap Mantido Por:** Tech Lead  
**Última Revisão:** 2025-10-01  
**Próxima Revisão:** Fim de cada sprint  
**Versão:** 1.0
