# 🔗 Matriz de Dependências - SPECs 1-10

**Data:** 2025-10-01  
**Objetivo:** Mapeamento completo de dependências entre especificações

---

## 📊 Matriz Visual de Dependências

```
           │ 0001 │ 0002 │ 0003 │ 0004 │ 0005 │ 0007 │ 0008 │ 0009 │ 0010 │
───────────┼──────┼──────┼──────┼──────┼──────┼──────┼──────┼──────┼──────┤
SPEC-0001  │  -   │      │      │      │      │      │      │      │      │
SPEC-0002  │      │  -   │      │      │      │      │      │      │      │
SPEC-0003  │      │      │  -   │      │      │      │      │  ○   │      │
SPEC-0004  │      │      │      │  -   │      │      │      │  ○   │      │
SPEC-0005  │      │      │  ●   │      │  -   │      │      │  ●   │      │
SPEC-0007  │      │      │      │      │      │  -   │      │      │      │
SPEC-0008  │      │      │      │      │  ●   │      │  -   │  ●   │      │
SPEC-0009  │      │      │      │      │      │      │      │  -   │      │
SPEC-0010  │      │      │      │      │  ○   │      │      │  ●   │  -   │

Legenda:
● = Dependência Forte (bloqueante)
○ = Dependência Fraca (recomendado, não bloqueante)
```

**Leitura:** Linha depende de Coluna  
**Exemplo:** SPEC-0005 depende fortemente de SPEC-0003 e SPEC-0009

---

## 🔍 Detalhamento de Dependências

### SPEC-0001: User Registration
**Status:** 🟢 Sem dependências (implementação já existe)

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| Nenhuma | - | - | Funcionalidade standalone já implementada |

**Pode ser implementada:** A qualquer momento (prioridade baixa por já existir)

---

### SPEC-0002: CORS Support
**Status:** 🟢 Sem dependências

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| Nenhuma | - | - | Configuração de infraestrutura independente |

**Pode ser implementada:** A qualquer momento

**Nota:** Embora não tenha dependências, é recomendado após SPEC-0005 para garantir que erros de CORS sejam tratados adequadamente.

---

### SPEC-0003: Problem Details (RFC 7807)
**Status:** 🟡 Dependência fraca

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| SPEC-0009 | Fraca | REQ-02 (logging) | Logs estruturados de conversões, mas não bloqueante |

**Pode ser implementada:** Após SPEC-0009 (recomendado) ou em paralelo

**Benefícios de Sequência:**
- ✅ Logs de conversão para Problem Details já estruturados
- ✅ TraceId disponível imediatamente

---

### SPEC-0004: Health Checks
**Status:** 🟡 Dependência fraca

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| SPEC-0009 | Fraca | NFR-01 (logging) | Logs de status unhealthy, mas não bloqueante |

**Pode ser implementada:** A qualquer momento (após SPEC-0009 é melhor)

**Benefícios de Sequência:**
- ✅ Logs de health check failures estruturados
- ✅ Correlação de problemas facilitada

---

### SPEC-0005: Error Handling Middleware
**Status:** 🔴 Dependências fortes

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| SPEC-0009 | Forte | REQ-04 | Logging estruturado obrigatório para erros |
| SPEC-0003 | Forte | REQ-03 | Integração com Problem Details quando habilitado |

**Pode ser implementada:** Apenas após SPEC-0009 e SPEC-0003

**Fluxo de Dependência:**
```
SPEC-0009 (Logging) ──┐
                      ├──> SPEC-0005 (Error Middleware)
SPEC-0003 (ProbDet) ──┘
```

**Bloqueios se não atendidas:**
- ❌ Sem SPEC-0009: Logs de erro não estruturados, dificulta troubleshooting
- ❌ Sem SPEC-0003: Formato de erro inconsistente, quebra REQ-03

---

### SPEC-0007: System Parameters
**Status:** 🟢 Sem dependências

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| Nenhuma | - | - | Funcionalidade standalone com entidade própria |

**Pode ser implementada:** A qualquer momento

**Nota:** Beneficia de SPEC-0011 (RBAC) para proteção de endpoints admin, mas não é dependência bloqueante.

---

### SPEC-0008: Rate Limiting
**Status:** 🔴 Dependências fortes

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| SPEC-0009 | Forte | REQ-05 | Logging de bloqueios obrigatório |
| SPEC-0005 | Forte | REQ-04 | Retorno de 429 com Problem Details via middleware |

**Pode ser implementada:** Apenas após SPEC-0009 e SPEC-0005

**Fluxo de Dependência:**
```
SPEC-0009 (Logging) ──┐
                      ├──> SPEC-0005 (Error Middleware) ──> SPEC-0008 (Rate Limit)
SPEC-0003 (ProbDet) ──┘
```

**Bloqueios se não atendidas:**
- ❌ Sem SPEC-0009: Não há logs de bloqueios, impossível auditar/debugar
- ❌ Sem SPEC-0005: Resposta 429 não segue padrão Problem Details

---

### SPEC-0009: Structured Logging
**Status:** 🟢 Sem dependências (BASE)

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| Nenhuma | - | - | Base de observabilidade para todas as outras |

**Pode ser implementada:** Imediatamente (PRIMEIRA SPEC RECOMENDADA)

**Importância:**
- ✅ Habilita observabilidade para todas as outras specs
- ✅ Sem bloqueadores
- ✅ Alta prioridade (fundação)

---

### SPEC-0010: Metrics (Telemetry)
**Status:** 🟡 Dependências fracas/moderadas

| Depende de | Tipo | Requisito | Justificativa |
|------------|------|-----------|---------------|
| SPEC-0009 | Forte | Contexto | TraceId e correlação para métricas |
| SPEC-0005 | Fraca | REQ-07 (opcional) | Métricas de erros por tipo/status |

**Pode ser implementada:** Após SPEC-0009 (mínimo), melhor após SPEC-0005

**Fluxo de Dependência:**
```
SPEC-0009 (Logging) ──> SPEC-0010 (Metrics)
                            ↑
SPEC-0005 (opcional) ───────┘
```

**Benefícios de Sequência:**
- ✅ Contexto de logging disponível para enriquecimento de métricas
- ✅ Métricas de erro complementam logs

---

## 📈 Níveis de Dependência

### Nível 0: Sem Dependências (Implementáveis Imediatamente)
```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│ SPEC-0009   │   │ SPEC-0002   │   │ SPEC-0007   │   │ SPEC-0001   │
│ (Logging)   │   │ (CORS)      │   │ (SysParams) │   │ (UserReg)   │
└─────────────┘   └─────────────┘   └─────────────┘   └─────────────┘
```

**Características:**
- ✅ Não bloqueadas por outras specs
- ✅ Podem iniciar a qualquer momento
- ✅ SPEC-0009 é base recomendada

---

### Nível 1: Dependência Fraca (Recomendado Após Base)
```
┌─────────────┐
│ SPEC-0009   │ ────────┐
└─────────────┘         │
                        ▼
              ┌─────────────────┐   ┌─────────────┐
              │ SPEC-0003       │   │ SPEC-0004   │
              │ (ProblemDetail) │   │ (Health)    │
              └─────────────────┘   └─────────────┘
```

**Características:**
- ⚠️ Funcionam sem dependências, mas melhor após SPEC-0009
- ✅ Podem ser implementadas em paralelo entre si

---

### Nível 2: Dependência Forte (Bloqueadas)
```
┌─────────────┐         ┌─────────────────┐
│ SPEC-0009   │ ──────> │ SPEC-0003       │
└─────────────┘         └─────────────────┘
                                │
                                ▼
                        ┌─────────────┐
                        │ SPEC-0005   │
                        └─────────────┘
```

**Características:**
- ❌ Bloqueadas até dependências serem atendidas
- ⚠️ Implementação fora de ordem causa problemas

---

### Nível 3: Dependência em Cadeia (Última Onda)
```
┌─────────────┐         ┌─────────────────┐         ┌─────────────┐
│ SPEC-0009   │ ──────> │ SPEC-0003       │ ──────> │ SPEC-0005   │
└─────────────┘         └─────────────────┘         └─────────────┘
                                                             │
                                    ┌────────────────────────┴────────┐
                                    ▼                                 ▼
                            ┌─────────────┐                 ┌─────────────┐
                            │ SPEC-0008   │                 │ SPEC-0010   │
                            │ (RateLimit) │                 │ (Metrics)   │
                            └─────────────┘                 └─────────────┘
```

**Características:**
- ❌ Última onda de implementação
- ✅ Beneficiam de toda stack de observabilidade

---

## 🚨 Riscos de Implementação Fora de Ordem

### Cenário 1: Implementar SPEC-0005 antes de SPEC-0003
**Problema:**
```
ErrorHandlingMiddleware sem Problem Details
    ↓
REQ-03 não pode ser atendido
    ↓
Fallback para JSON simples (menos consistente)
```

**Impacto:** 🟡 Médio
- Formato de erro inconsistente
- Retrabalho para integrar Problem Details depois
- Clientes podem depender de formato provisório

**Mitigação:** Implementar SPEC-0003 primeiro ou em paralelo

---

### Cenário 2: Implementar SPEC-0008 antes de SPEC-0009
**Problema:**
```
Rate Limiting sem Logging Estruturado
    ↓
REQ-05 (logs de bloqueio) não atendido
    ↓
Impossível auditar/debugar bloqueios
```

**Impacto:** 🔴 Alto
- Sem visibilidade de quem está sendo bloqueado
- Impossível identificar false positives
- Troubleshooting inviável

**Mitigação:** Implementar SPEC-0009 primeiro (obrigatório)

---

### Cenário 3: Implementar SPEC-0010 antes de SPEC-0009
**Problema:**
```
Metrics sem Logging Estruturado
    ↓
Sem contexto de correlação (TraceId)
    ↓
Métricas desconectadas de logs
```

**Impacto:** 🟡 Médio
- Métricas funcionam, mas sem contexto rico
- Dificulta correlação de métricas com eventos
- Experiência de debugging inferior

**Mitigação:** Implementar SPEC-0009 primeiro (fortemente recomendado)

---

## ✅ Validação de Ordem

Use este checklist antes de iniciar cada SPEC:

### Para SPEC-0001 (User Registration)
- [ ] Nenhuma dependência - pode iniciar

### Para SPEC-0002 (CORS)
- [ ] Nenhuma dependência - pode iniciar

### Para SPEC-0003 (Problem Details)
- [ ] (Recomendado) SPEC-0009 implementada
- [ ] Logs estruturados disponíveis

### Para SPEC-0004 (Health Checks)
- [ ] (Recomendado) SPEC-0009 implementada
- [ ] Logs estruturados disponíveis

### Para SPEC-0005 (Error Middleware)
- [ ] ✅ SPEC-0009 implementada (obrigatório)
- [ ] ✅ SPEC-0003 implementada (obrigatório)
- [ ] Logs estruturados funcionando
- [ ] Problem Details configurado

### Para SPEC-0007 (System Parameters)
- [ ] Nenhuma dependência - pode iniciar

### Para SPEC-0008 (Rate Limiting)
- [ ] ✅ SPEC-0009 implementada (obrigatório)
- [ ] ✅ SPEC-0005 implementada (obrigatório)
- [ ] Logs estruturados funcionando
- [ ] Error Middleware capturando exceções

### Para SPEC-0009 (Logging)
- [ ] Nenhuma dependência - pode iniciar

### Para SPEC-0010 (Metrics)
- [ ] ✅ SPEC-0009 implementada (obrigatório)
- [ ] (Recomendado) SPEC-0005 implementada
- [ ] Logs estruturados funcionando

---

## 🎯 Caminho Crítico

Sequência mínima viável para máximo valor:

```
1. SPEC-0009 (Logging)
   ↓
2. SPEC-0003 (Problem Details)
   ↓
3. SPEC-0005 (Error Middleware)
   ↓
4. SPEC-0008 (Rate Limiting)

Paralelo a qualquer momento:
- SPEC-0002 (CORS)
- SPEC-0004 (Health Checks)
- SPEC-0007 (System Parameters)
- SPEC-0010 (Metrics)
- SPEC-0001 (User Registration)
```

**Tempo Mínimo:** 4-5 semanas (caminho crítico)  
**Tempo com Paralelização:** 6-7 semanas (incluindo specs paralelas)

---

## 📚 Referências

- **SPECs Originais:** `specs/SPEC-000[1-5,7-10]-*.md`
- **Ordem Recomendada Detalhada:** `docs/implementation-order-specs-1-10.md`
- **Roadmap Visual:** `docs/implementation-roadmap-specs-1-10.md`
- **Resumo Executivo:** `docs/implementation-order-summary.md`

---

**Mantido por:** Tech Lead  
**Última Atualização:** 2025-10-01  
**Versão:** 1.0
