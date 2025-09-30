---
spec:
  id: SPEC-0002
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: medium
  quality_order: [security, reliability, performance, observability, delivery_speed, cost]
  tags: [api, cors, security, configuration]
---

# 1. Objetivo
Permitir que clientes web autorizados consumam a Vanq API a partir de origens diferentes do domínio da API, mantendo controles de segurança e configurabilidade por ambiente.

# 2. Escopo
## 2.1 In
- Definir política CORS nomeada aplicada globalmente aos endpoints atuais (`/auth/*`).
- Carregar origens, métodos e cabeçalhos permitidos via configuração (appsettings / variáveis de ambiente).
- Tornar política mais permissiva em ambiente de desenvolvimento, mantendo restrições em produção.
- Atualizar documentação (README/Scalar) indicando requisitos de configuração de CORS.
- Cobrir cenário com testes automatizados mínimos (smoke) validando cabeçalhos CORS.

## 2.2 Out
- Suporte avançado a configuração dinâmica por cliente/tenant.
- Interface administrativa para gerenciar origens em tempo real.
- Cache distribuído ou personalização de tempo de expiração de preflight (`Access-Control-Max-Age`).

## 2.3 Não Fazer
- Habilitar `AllowAnyOrigin` em produção.
- Liberar credenciais (cookies/autenticação) sem validação explícita de origens confiáveis.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Registrar política CORS nomeada (`vanq-default-cors`) com origens configuráveis via `IConfiguration`. | MUST |
| REQ-02 | Aplicar a política globalmente no pipeline (`app.UseCors`) antes de autenticação/autorização. | MUST |
| REQ-03 | Permitir configurar métodos e cabeçalhos permitidos; padrão deve incluir os utilizados pelos endpoints atuais. | MUST |
| REQ-04 | Suportar modo de desenvolvimento com `AllowAnyOrigin/AllowAnyHeader/AllowAnyMethod` guardado por `app.Environment.IsDevelopment()`. | SHOULD |
| REQ-05 | Documentar os passos de configuração de CORS (appsettings e variáveis) no repositório. | SHOULD |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Segurança | Somente origens listadas para produção devem ser aceitas. | Auditoria mostra 0 respostas com `Access-Control-Allow-Origin: *` em produção |
| NFR-02 | Observabilidade | Logar aviso quando origem não autorizada tentar acesso (nível debug/trace). | Evento registrado com origem rejeitada |
| NFR-03 | Performance | Responder preflight em p95 < 120ms. | Monitoramento dev/qa confirma |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Apenas origens com esquema HTTPS são válidas em ambientes produtivos. |
| BR-02 | Comparação de origens deve ignorar trailing slash e case da parte host. |
| BR-03 | Quando `AllowCredentials` estiver habilitado, não permitir `AllowAnyOrigin`. |

# 6. Novas Entidades
Nenhuma nova entidade é necessária.

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
| Domain | Nenhuma. | |
| Application | Ajustes opcionais em serviços para expor configurações (se necessário). | Avaliar se `IDateTimeProvider` é usado nos logs. |
| Infrastructure | Registro de serviços CORS em `AddInfrastructure` não é necessário; ficará em API. | |
| API | Configurar `AddCors` e `UseCors`, carregar configurações e atualizar documentação Scalar/OpenAPI se pertinente. | |

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | * | * | Conforme endpoint | REQ-01,REQ-02 | Cabeçalhos `Access-Control-Allow-*` presentes conforme política | 403 origem não permitida (quando aplicável) |

# 9. Segurança & Performance
- Segurança: restringir origens por ambiente; registrar tentativas negadas em log estruturado.
- Performance: evitar lógica pesada nas respostas de preflight; reutilizar policy name.
- Observabilidade: adicionar métricas/eventos para contagem de bloqueios (se viável, contador em `Vanq.Infrastructure` ou log com `event=cors-blocked`).

# 10. i18n
Não.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | cors-relaxed | API | Permitir habilitar política permissiva temporária (dev/staging) | Fallback: política restritiva padrão |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Definir estrutura `Cors` em `appsettings` (origens, métodos, headers). | - | REQ-01,REQ-03 |
| TASK-02 | Registrar `AddCors` com leitura das configurações. | TASK-01 | REQ-01,REQ-03 |
| TASK-03 | Aplicar `app.UseCors("vanq-default-cors")` antes de auth/authorization. | TASK-02 | REQ-02 |
| TASK-04 | Implementar fallback permissivo condicionado a `IsDevelopment()`. | TASK-02 | REQ-04 |
| TASK-05 | Atualizar documentação (`docs/` ou README) descrevendo configuração de CORS. | TASK-02 | REQ-05 |
| TASK-06 | Adicionar teste de integração validando cabeçalhos CORS para origem autorizada e bloqueio para não autorizada. | TASK-03 | REQ-01..03 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Política `vanq-default-cors` retorna `Access-Control-Allow-Origin` igual à origem configurada. |
| REQ-02 | `OPTIONS` preflight inclui cabeçalhos padrão e responde 200 para origem permitida. |
| REQ-03 | Requisição de origem não listada não retorna cabeçalhos `Access-Control-Allow-*`. |
| REQ-04 | Ambiente dev permite qualquer origem sem necessidade de configuração manual. |
| REQ-05 | Documentação descreve como adicionar novas origens e comportamento dev/prod. |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Integration | REQ-01,REQ-02 | Requisição OPTIONS com origem permitida retorna 200 e cabeçalhos configurados. |
| TEST-02 | Integration | REQ-03 | Requisição OPTIONS com origem não permitida não inclui cabeçalhos CORS. |
| TEST-03 | Unit | REQ-04 | Verifica configuração relaxada em ambiente de desenvolvimento. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Nomear política padrão | Usar `vanq-default-cors` | `default` genérico | Facilita identificação e testes |
| DEC-02 | Fonte de configuração | `appsettings` com override via env vars | Banco de dados / Secrets Manager | Simplicidade e compliance com Twelve-Factor |
| DEC-03 | Tratamento dev vs prod | Habilitar relaxamento só em `IsDevelopment()` | Feature flag permanente | Menos risco de exposição indevida |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Quais domínios (produção e staging) devem ser liberados inicialmente? | owner | Aberto |
| QST-02 | Precisamos expor métrica dedicada para bloqueios CORS? | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0002 adicionando suporte CORS global com política `vanq-default-cors`, carregando origens/métodos/cabeçalhos de configuração, respeitando modo dev relaxado e atualizando documentação. Considerar NFR-01..03 e aplicar feature flag FLAG-01 caso necessário. Não criar entidades novas. Manter logging/observabilidade para origens bloqueadas.

Fim.
