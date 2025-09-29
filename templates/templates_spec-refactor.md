---
spec:
  id: SPEC-REFACT-XXX
  type: refactor
  version: 0.1.0
  status: draft
  owner: <github-user>
  created: YYYY-MM-DD
  updated: YYYY-MM-DD
---

# 1. Motivação
[Problema atual: dívida técnica, complexidade, desempenho]

# 2. Objetivo Mensurável
Ex.: Reduzir complexidade ciclomática média de X para Y.

# 3. Escopo
Inclui:  
Exclui: (não quebrar API pública, não alterar contratos)

# 4. Restrições
- Não alterar comportamento externo observável sem nota explícita.

# 5. Requisitos (Refactor Outcomes)
| ID | Descrição | Métrica |
|----|-----------|---------|
| REQ-01 | Reduzir acoplamento módulo X | Medir dependências antes/depois |

# 6. NFR Relevantes
| ID | Categoria | Descrição | Meta |
|----|-----------|-----------|------|
| NFR-01 | Performance | Não degradar p95 > +5% | |

# 7. Estratégia
[Incremental? Branch isolada? Feature flag sombra?]

# 8. Riscos
| ID | Descrição | Prob. | Impacto | Mitigação |
|----|-----------|-------|---------|-----------|

# 9. Métricas Antes/Depois (Baseline)
| Métrica | Antes | Objetivo Depois | Depois (preencher) |
|---------|-------|-----------------|--------------------|

# 10. Tarefas
| ID | Descrição | Dependências |
|----|-----------|--------------|
| TASK-01 | | - |

# 11. Testes de Regressão
| TEST | Escopo | Observação |
|------|--------|------------|

# 12. Decisões
| ID | Contexto | Decisão | Consequência |
|----|----------|--------|--------------|

# 13. Prompt Copilot
Copilot: Refatorar conforme REQ-01.. preservando comportamento externo (tests existentes devem continuar verdes). Não introduzir dependências novas sem justificativa.

Fim.