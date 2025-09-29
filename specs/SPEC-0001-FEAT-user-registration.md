---
spec:
  id: SPEC-0001
  type: feature
  version: 0.1.0
  status: draft          # draft | reviewing | approved | deprecated
  owner: nuno-simao
  created: 2025-09-29
  updated: 2025-09-29
  priority: high
  quality_order: [performance, security, reliability, observability, delivery_speed, cost]
  tags: [auth, users, onboarding]
---

# 1. Objetivo
Estabelecer (e formalizar retroativamente) a especificação do fluxo de registro de usuário (onboarding inicial), garantindo segurança, base para expansão de perfis e padronização de mensagens com i18n e observabilidade mínima.

# 2. Escopo
## 2.1 In
- Registro de novo usuário (email + password)
- Retorno de tokens (access + refresh) pós-registro
- Validações mínimas de senha e email
- Emissão de métricas básicas (contagem de registros)
- Suporte inicial de i18n para mensagens de validação (pt-BR / en-US)
- Feature Flag para eventual bloqueio rápido do fluxo (circuit breaker de registro)

## 2.2 Out
- Fluxo de confirmação de e-mail
- Reset / recuperação de senha
- Perfis estendidos (avatar, timezone, etc.)
- Rate limiting (apenas monitorar por enquanto)
- Auditoria avançada

## 2.3 Não Fazer
- Envio de e-mail transacional
- Persistir dados extras além do mínimo (Id, Email, PasswordHash, CreatedAt)

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|------------------------------|
| REQ-01 | Registrar usuário com email único e senha válida retornando tokens | MUST |
| REQ-02 | Negar registro se email já existente | MUST |
| REQ-03 | Gerar refresh token associado ao usuário | MUST |
| REQ-04 | Registrar data/hora de criação do usuário | SHOULD |
| REQ-05 | Expor mensagem de validação traduzível (email inválido, senha fraca) | SHOULD |
| REQ-06 | Contabilizar métrica de sucesso de registro | SHOULD |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Performance | Latência p95 do POST /auth/register | < 180ms |
| NFR-02 | Segurança | Armazenar senha usando hash robusto (ex. BCrypt com custo configurado) | Custo >= baseline definido |
| NFR-03 | Observabilidade | Log estruturado de tentativa (sucesso/falha) sem expor dados sensíveis | Campo event=UserRegistration |
| NFR-04 | Confiabilidade | Nenhuma condição de corrida resultando em usuários duplicados | Unicidade garantida por índice + transação |
| NFR-05 | i18n | Mensagens de validação disponíveis em pt-BR e fallback en-US | 100% mensagens críticas cobertas |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Email deve ser único (case-insensitive) |
| BR-02 | Senha mínima 8 caracteres, conter ao menos 1 letra e 1 dígito |
| BR-03 | Registro bem sucedido sempre gera par (accessToken, refreshToken) |
| BR-04 | Token de refresh é single-use rotacionado após login/refresh subsequente |

# 6. Novas Entidades
(Não criar nova entidade se User já existe; caso não exista, ENT-01 define.)

| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | User | Representar credenciais e identificação básica do usuário | Evitar campos desnecessários inicialmente |

## 6.1 Campos (Somente Entidades Novas ou Acrescentados)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| User | Id | Guid | No | PK |
| User | Email | string(256) | No | Unique (case-insensitive) |
| User | PasswordHash | string | No | Hash BCrypt |
| User | CreatedAt | DateTime (UTC) | No | Default now |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Entidade User (se não existente) | |
| Application | Serviço de Auth já existente: garantir criação + validação | Reuso de IAuthService |
| Infrastructure | Repositório/DbContext: índice único em Email | |
| API | Endpoint POST /auth/register já mapeado | Ajustar validações/i18n se necessário |

# 8. API (Se aplicável)
| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | POST | /auth/register | Anônima | REQ-01..03 | 200 AuthResponseDto | 400,409 |

## 8.1 Request Exemplo
```json
{
  "email": "user@example.com",
  "password": "Passw0rd!"
}
```

## 8.2 Response Sucesso
```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<token>",
  "expiresIn": 3600
}
```

## 8.3 Response Erro (Exemplos)
Email existente:
```json
{
  "errorCode": "ERR-USER-ALREADY-EXISTS",
  "message": "E-mail já cadastrado"
}
```

Validação senha fraca:
```json
{
  "errorCode": "ERR-WEAK-PASSWORD",
  "message": "Senha inválida"
}
```

## 8.4 Erros
| Código | HTTP | Mensagem (pt-BR) | Mensagem (en-US) | Causa |
|--------|------|------------------|------------------|-------|
| ERR-USER-ALREADY-EXISTS | 409 | E-mail já cadastrado | Email already registered | Conflito unicidade |
| ERR-WEAK-PASSWORD | 400 | Senha inválida | Invalid password | Regras BR-02 |
| ERR-INVALID-EMAIL | 400 | E-mail inválido | Invalid email | Formato incorreto |

# 9. Segurança & Performance
- Segurança: Hash BCrypt, não logar password/email completo (parcial ofuscado).
- Performance: Índice em Email; evitar leitura redundante.
- Observabilidade: Log de (attempt, success, conflict), métrica counter user_registration_total{status="success|conflict"}.

# 10. i18n
Usa: Sim  
Chaves sugeridas:
| Chave | pt-BR | en-US |
|-------|-------|-------|
| validation.email.invalid | E-mail inválido | Invalid email |
| validation.password.weak | Senha inválida | Weak password |
| conflict.user.exists | E-mail já cadastrado | Email already registered |

Fallback: en-US.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|-----------|----------|
| FLAG-01 | user-registration-enabled | API | Kill-switch (on/off) | Desligado → 503/temporarily disabled |

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Verificar existência índice único em Email | - | REQ-02 |
| TASK-02 | Implementar validação password (regex ou policy) | - | REQ-01,REQ-02 |
| TASK-03 | Internacionalizar mensagens (pt-BR + en-US) | TASK-02 | REQ-05 |
| TASK-04 | Adicionar métrica user_registration_total | - | REQ-06 |
| TASK-05 | Implementar flag FLAG-01 no endpoint /auth/register | - | REQ-01 |
| TASK-06 | Ajustar mapeamento de erros para códigos definidos | TASK-02 | REQ-02,REQ-05 |
| TASK-07 | Log estruturado (event=UserRegistration) | TASK-06 | NFR-03 |
| TASK-08 | Testes de sucesso e conflito (cenários básicos) | TASK-06 | REQ-01..03 |
| TASK-09 | Documentar i18n no README (seção auth) | TASK-03 | REQ-05 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Registro retorna accessToken e refreshToken válidos |
| REQ-02 | Repetição de email retorna 409 e ERR-USER-ALREADY-EXISTS |
| REQ-03 | RefreshToken persistido e associado ao usuário |
| REQ-05 | Mensagens suportam pt-BR e fallback en-US |
| REQ-06 | Métrica incrementa em sucesso e não em falha |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-02 | Verifica conflito de email |
| TEST-02 | Unit | REQ-01,REQ-02 | Validação de senha |
| TEST-03 | Integration | REQ-01..03 | Fluxo completo registro sucesso |
| TEST-04 | Integration | REQ-02 | Registro duplicado |
| TEST-05 | Integration | REQ-05 | i18n fallback en-US |
| TEST-06 | Integration | REQ-06 | Métrica incrementa |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Padronização de erro 409 para email duplicado | Usar 409 Conflict | 400 genérico | Sem ambiguidade de validação |
| DEC-02 | Hash de senha | BCrypt custo default X | Argon2, PBKDF2 | Simplicidade / trade-off Argon2 futuro |
| DEC-03 | i18n inicial | Somente pt-BR + fallback en-US | Adicionar mais idiomas agora | Menos sobrecarga inicial |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Confirmar custo BCrypt alvo (work factor) | owner | Aberto |
| QST-02 | Definir se métricas expostas publicamente | owner | Aberto |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0001 (User Registration) cobrindo REQ-01..REQ-06:
- Validar e registrar usuário (email única, senha forte).
- Retornar tokens (access + refresh).
- Aplicar flag FLAG-01: se desligada retornar 503 com JSON { \"errorCode\": \"ERR-FEATURE-DISABLED\" }.
- Mapear erros conforme tabela (incluindo 409 para email duplicado).
- Adicionar métrica user_registration_total (labels status=success|conflict|error).
- Log estruturado event=UserRegistration (fields: status, userId? (somente no sucesso), emailHash ou emailMasked).
- Implementar i18n das mensagens (pt-BR / en-US fallback).
Não criar entidades além de ENT-01 (se ainda não existir). Respeitar NFR-01 p95 < 180ms, NFR-02 hashing forte, NFR-03 logs completos, NFR-05 i18n.

Fim.