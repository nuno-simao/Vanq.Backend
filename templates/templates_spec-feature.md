--- 
spec:
  id: SPEC-FEAT-XXX
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: <github-user>
  created: YYYY-MM-DD
  updated: YYYY-MM-DD
  priority: medium
  quality_order: [performance, security, reliability, observability, delivery_speed, cost]
---

# 1. Objetivo
[Descrição curta do benefício de negócio]

# 2. Escopo
## 2.1 In
- 
## 2.2 Out
- 
## 2.3 Não Fazer
- 

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|--------------------------------|
| REQ-01 | | MUST |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | | ex.: p95 < 120ms |
| NFR-02 | Segurança | | |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | |

# 6. Novas Entidades
| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | | | |

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | | |
| Application | | |
| Infrastructure | | |
| API | | |

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /v1/... | JWT | REQ-01 | 200 DTO | 400,401,404 |

# 9. Segurança & Performance
- Segurança: [ex.: exigir JWT com escopo X]
- Performance: [ex.: evitar N+1; índice em campo Y]
- Observabilidade: [logs estruturados em operações críticas]

# 10. i18n
Usa? (Sim/Não). Se Sim: listar mensagens/strings críticas.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | feature-nome | API | Release gradual | Desliga → caminho antigo |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | | - | REQ-01 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-01 | |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | | | | |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | | | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-FEAT-XXX cobrindo REQ-01.., criando ENT-01, endpoints API-01 (se aplicável), seguindo padrão de retorno atual (DTO direto em sucesso, códigos HTTP adequados nos erros). Considerar NFR-01 (performance) e NFR-02 (segurança). Não criar entidades além das listadas. Aplicar feature flag FLAG-01 se marcado.

Fim.