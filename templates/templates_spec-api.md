---
spec:
  id: SPEC-API-XXX
  type: api
  version: 0.1.0
  status: draft
  owner: <github-user>
  created: YYYY-MM-DD
  updated: YYYY-MM-DD
---

# 1. Objetivo
[Definir novos endpoints ou evolução de contratos]

# 2. Escopo (In/Out)
In:  
Out:  

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade |
|----|-----------|------------|
| REQ-01 | | MUST |

# 4. Requisitos Não Funcionais (Foco nas Prioridades)
| ID | Categoria | Descrição | Métrica |
|----|-----------|-----------|--------|
| NFR-01 | Performance | | p95 < 120ms |

# 5. Contratos / Endpoints
| ID | Método | Rota | Auth | Body | Sucesso (HTTP/DTO) | Erros |
|----|--------|------|------|------|--------------------|-------|
| API-01 | POST | /v1/... | JWT | RequestDto | 201 ResourceDto | 400,401 |

## 5.1 Request Exemplo
```json
{
  "campo": "valor"
}
```

## 5.2 Response Sucesso
```json
{
  "id": "..."
}
```

## 5.3 Response Erro (Padrão)
```json
{
  "errorCode": "ERR-01",
  "message": "Descrição"
}
```

## 5.4 Erros
| Código | HTTP | Mensagem | Causa |
|--------|------|----------|-------|
| ERR-01 | 400 | | |

# 6. Versionamento
[ex.: /v1 permanece; mudança compatível?]

# 7. Segurança
- Autorização: [Policies / Claims]
- Rate limiting (se aplicável)

# 8. i18n
Mensagens de erro traduzíveis? (Sim/Não). Estratégia.

# 9. Feature Flags
| ID | Nome | Escopo | Tipo | Fallback |
|----|------|--------|------|----------|

# 10. Impactos em Domínio/Infra
| Camada | Impacto |
|--------|---------|
| Domain | |
| Infrastructure | |

# 11. Tarefas
| ID | Descrição | REQs | Dependências |
|----|-----------|------|--------------|
| TASK-01 | | REQ-01 | - |

# 12. Testes
| TEST | Tipo | REQs | Descrição |
|------|------|------|-----------|
| TEST-01 | Integration | REQ-01 | Sucesso |
| TEST-02 | Integration | REQ-01 | Erro 400 |

# 13. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|

# 14. Prompt Copilot
Copilot: Criar/alterar endpoints API-01..., seguindo padrão de autenticação já usado em AuthEndpoints (JWT + .RequireAuthorization() quando necessário). Retornar DTO direto em sucesso, erros com códigos HTTP apropriados e erro JSON `{ \"errorCode\": \"...\", \"message\": \"...\" }` conforme tabela. Considerar NFR-01.

Fim.