---
spec:
  id: SPEC-0011-FEAT-role-based-access-control
  type: feature
  version: 0.1.0
  status: approved
  owner: nuno-simao
  created: 2025-09-30
  updated: 2025-09-30
  priority: high
  quality_order: [security, relity, performance, observability, delivery_speed, cost]
  depends_on: [SPEC-0006]
---

# 1. Objetivo
Adicionar um modelo completo de Role-Based Access Control (RBAC) ao Vanq.Backend para proteger recursos sensíveis, garantindo que apenas usuários com permissões apropriadas executem ações críticas e permitindo governança centralizada de acessos.

# 2. Escopo
## 2.1 In
- Modelagem de entidades `Role`, `Permission` e relacionamentos com `User`.
- Persistência em banco (EF Core + migrações) para armazenar funções, permissões e atribuições.
- Serviços de aplicação para gerenciar atribuições de roles e permissões.
- Endpoints administrativos para CRUD de roles/permissões e atribuição a usuários.
- Inclusão de claims de roles/permissões nos tokens JWT e validação em tempo de execução.

## 2.2 Out
- Interface gráfica para gestão de acessos (ficará para front-end).
- Integração com provedores externos de identidade ou SSO.
- Auditoria avançada (logs detalhados, trilhas de auditoria externas).

## 2.3 Não Fazer
- Implementar Attribute-Based Access Control (ABAC) ou hierarquia de roles.
- Automatizar provisionamento de roles a partir de diretórios corporativos.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|--------------------------------|
| REQ-01 | Criar entidades de domínio e persistência para roles, permissions e seus relacionamentos com usuários. | MUST |
| REQ-02 | Disponibilizar serviços para atribuir/remover roles em usuários com validações e regras de segurança. | MUST |
| REQ-03 | Expor endpoints autenticados para CRUD de roles e permissions, restritos a usuários administrativos. | MUST |
| REQ-04 | Incluir roles e permissions relevantes nos tokens JWT e disponibilizar helper para verificação durante requisições. | MUST |
| REQ-05 | Implementar middleware/filtros que validem permissões declaradas pelos endpoints antes do processamento. | MUST |
| REQ-06 | Disponibilizar seeds iniciais (ex.: Admin, Manager, Viewer) com permissões padrão configuráveis. | SHOULD |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Segurança | Garantir que apenas usuários com permissão explícita consigam atingir endpoints protegidos. | 100% das rotas sensíveis requerem validação de permissão |
| NFR-02 | Performance | Resolver checks de permissão sem consultas redundantes (caching leve em memória no request). | Check de permissão em < 5ms p95 após a primeira avaliação |
| NFR-03 | Observabilidade | Logar tentativas de acesso negadas com dados mínimos (userId, roleIds, permission requisitada). | 100% das negações registradas com correlação |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Nomes de roles são únicos (case-insensitive) e não podem ser alterados para roles de sistema. |
| BR-02 | Permissões seguem padrão `dominio:recurso:acao` e são únicas. |
| BR-03 | Usuários devem possuir ao menos uma role ativa; na ausência de atribuição explícita, aplicar role padrão `viewer`. |
| BR-04 | Roles marcadas como `IsSystemRole` não podem ser removidas nem perder permissões obrigatórias. |

# 6. Novas Entidades
| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | Role | Representar um agrupamento de permissões ligado a responsabilidades específicas. | Pode ser marcada como sistema para proteção extra. |
| ENT-02 | Permission | Descrever uma ação autorizada no sistema. | Utilizada para compor roles. |
| ENT-03 | RolePermission | Associação many-to-many entre roles e permissions. | Deve registrar quem adicionou e quando. |
| ENT-04 | UserRole | Associação many-to-many entre usuários e roles. | Deve registrar `AssignedBy` e `AssignedAt`. |

## 6.1 Campos (Somente Entidades Novas)
| Entidade | Campo | Tipo | Nullable | Regra / Constraint |
|----------|-------|------|----------|--------------------|
| Role | Id | Guid | Não | PK |
| Role | Name | string(100) | Não | Único, lowercase, validado por regex `^[a-z][a-z0-9-_]+$` |
| Role | DisplayName | string(120) | Não | Nome exibido em UI |
| Role | Description | string(300) | Sim | |
| Role | IsSystemRole | bool | Não | Default `false` |
| Role | SecurityStamp | string(64) | Não | Atualizar quando permissions mudarem |
| Role | CreatedAt | DateTimeOffset | Não | Definido por `IDateTimeProvider` |
| Role | UpdatedAt | DateTimeOffset | Não | Atualizado automaticamente |
| Permission | Id | Guid | Não | PK |
| Permission | Name | string(150) | Não | Único, lowercase, padrão `dominio:recurso:acao` |
| Permission | DisplayName | string(150) | Não | |
| Permission | Description | string(300) | Sim | |
| Permission | CreatedAt | DateTimeOffset | Não | |
| RolePermission | RoleId | Guid | Não | FK Role |
| RolePermission | PermissionId | Guid | Não | FK Permission |
| RolePermission | AddedBy | Guid | Não | Usuário que realizou alteração |
| RolePermission | AddedAt | DateTimeOffset | Não | |
| UserRole | UserId | Guid | Não | FK User |
| UserRole | RoleId | Guid | Não | FK Role |
| UserRole | AssignedBy | Guid | Não | Usuário administrador responsável |
| UserRole | AssignedAt | DateTimeOffset | Não | |
| UserRole | RevokedAt | DateTimeOffset | Sim | Preenchido quando role removida |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Novas entidades agregadas, atualizações em `User` para expor roles ativos. | Avaliar invariantes e métodos de fábrica. |
| Application | Casos de uso/serviços para gestão de roles e checagem de permissões. | Integrar com `IAuthService` e criar `IPermissionChecker`. |
| Infrastructure | Novas configurações EF Core, repositórios específicos, migrações e seeds. | Utilizar transações ao atualizar roles/permissões. |
| API | Novos endpoints protegidos, políticas/middlewares para autorização granular. | Atualizar documentação Scalar com grupos `/roles`, `/permissions` e `/users` (DEC-04). |

# 8. API (Se aplicável)

**Nota:** Conforme DEC-04, endpoints RBAC estão organizados por domínio de recurso (não sob `/auth`).

| ID | Método | Rota | Auth | REQs | Sucesso | Erros |
|----|--------|------|------|------|---------|-------|
| API-01 | GET | /roles | JWT + permissão `rbac:role:read` | REQ-03 | 200 List<RoleDto> | 401,403 |
| API-02 | POST | /roles | JWT + permissão `rbac:role:create` | REQ-03 | 201 RoleDto | 400,401,403 |
| API-03 | PATCH | /roles/{roleId} | JWT + permissão `rbac:role:update` | REQ-03 | 200 RoleDto | 400,401,403,404 |
| API-04 | DELETE | /roles/{roleId} | JWT + permissão `rbac:role:delete` | REQ-03, BR-04 | 204 | 400,401,403,404 |
| API-05 | GET | /permissions | JWT + permissão `rbac:permission:read` | REQ-03 | 200 List<PermissionDto> | 401,403 |
| API-06 | POST | /permissions | JWT + permissão `rbac:permission:create` | REQ-03 | 201 PermissionDto | 400,401,403 |
| API-07 | PATCH | /permissions/{permissionId} | JWT + permissão `rbac:permission:update` | REQ-03 | 200 PermissionDto | 400,401,403,404 |
| API-08 | DELETE | /permissions/{permissionId} | JWT + permissão `rbac:permission:delete` | REQ-03 | 204 | 400,401,403,404 |
| API-09 | POST | /users/{userId}/roles | JWT + permissão `rbac:user:role:assign` | REQ-02 | 204 | 400,401,403,404 |
| API-10 | DELETE | /users/{userId}/roles/{roleId} | JWT + permissão `rbac:user:role:revoke` | REQ-02 | 204 | 400,401,403,404 |

## 8.1 Contratos e payloads

### `RoleDto`
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `id` | Guid | Identificador da role. |
| `name` | string | Nome único (`snake-case`) utilizado internamente. |
| `displayName` | string | Nome amigável exibido em UI. |
| `description` | string? | Descrição opcional da responsabilidade. |
| `isSystemRole` | bool | Indica proteção adicional conforme BR-04. |
| `securityStamp` | string | Stamp para invalidação de tokens. |
| `createdAt` | DateTimeOffset | Data de criação. |
| `updatedAt` | DateTimeOffset | Última atualização. |
| `permissions` | PermissionDto[] | Lista completa de permissões associadas. |

### `PermissionDto`
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `id` | Guid | Identificador da permissão. |
| `name` | string | Código único no formato `dominio:recurso:acao`. |
| `displayName` | string | Nome amigável. |
| `description` | string? | Texto explicativo opcional. |
| `createdAt` | DateTimeOffset | Data de criação. |

### `POST /auth/roles`
Requer permissão `rbac:role:create`.

| Campo | Tipo | Obrigatório | Observações |
|-------|------|-------------|-------------|
| `name` | string | Sim | Lowercase, regex `^[a-z][a-z0-9-_]+$`. |
| `displayName` | string | Sim | Nome exibido. |
| `description` | string? | Não | Descrição opcional. |
| `isSystemRole` | bool | Sim | Impede exclusão/acréscimo inválido de permissões obrigatórias. |
| `permissions` | string[] | Sim | Lista de códigos de permissão a anexar. |

### `PATCH /auth/roles/{roleId}`
Requer permissão `rbac:role:update`.

| Campo | Tipo | Obrigatório | Observações |
|-------|------|-------------|-------------|
| `displayName` | string | Sim | Nome exibido atualizado. |
| `description` | string? | Não | Descrição opcional. |
| `permissions` | string[] | Sim | Lista completa desejada (substitui a atual). |

### `POST /auth/permissions`
Requer permissão `rbac:permission:create`.

| Campo | Tipo | Obrigatório | Observações |
|-------|------|-------------|-------------|
| `name` | string | Sim | Único, formato `dominio:recurso:acao`. |
| `displayName` | string | Sim | Nome amigável. |
| `description` | string? | Não | Texto explicativo opcional. |

### `PATCH /auth/permissions/{permissionId}`
Requer permissão `rbac:permission:update`.

| Campo | Tipo | Obrigatório | Observações |
|-------|------|-------------|-------------|
| `displayName` | string | Sim | Nome amigável revisado. |
| `description` | string? | Não | Descrição opcional. |

### `POST /auth/users/{userId}/roles`
Requer permissão `rbac:user:role:assign`.

| Campo | Tipo | Obrigatório | Observações |
|-------|------|-------------|-------------|
| `roleId` | Guid | Sim | Role a ser atribuída; valida BR-04. |

### `DELETE /auth/users/{userId}/roles/{roleId}`
Requer permissão `rbac:user:role:revoke`.

Sem payload (body vazio). A operação registra `RevokedAt` quando aplicável.

# 9. Segurança & Performance
- **Segurança:** Validar permissões antes da execução dos handlers, garantir rotação de `SecurityStamp` para invalidar tokens quando roles mudarem, e proteger endpoints administrativos com MFA opcional futuro.
- **Performance:** Carregar roles/permissões do usuário ao autenticar e cachear no token; para requisições longas, utilizar cache em memória por request e invalidar ao detectar `SecurityStamp` divergente.
- **Observabilidade:** Emitir logs estruturados para tentativas de acesso negadas e métricas de contagem de permissões utilizadas.

# 10. i18n
Não. Mensagens de erro seguem padrão atual em inglês, com possibilidade de tradução futura.

# 11. Feature Flags
| ID | Nome | Escopo | Estratégia | Fallback |
|----|------|--------|------------|----------|
| FLAG-01 | rbac-enabled | API | Release gradual por ambiente | Desligado → mantém autorização atual baseada apenas em claims existentes |

**Nota:** Este flag está definido no seed data do SPEC-0006 e substitui o antigo `RbacOptions.FeatureEnabled`.

# 12. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Modelar entidades e atualizações de domínio para RBAC. | - | REQ-01 |
| TASK-02 | Criar migrações e seeds iniciais para roles/permissões padrão. | TASK-01 | REQ-01, REQ-06 |
| TASK-03 | Implementar serviços e repositórios de roles/permissões. | TASK-01 | REQ-01, REQ-02 |
| TASK-04 | Atualizar emissão de tokens JWT com roles/permissões e validation pipeline. | TASK-03 | REQ-04 |
| TASK-05 | Construir endpoints de gestão e autorização baseada em permissões. | TASK-03 | REQ-02, REQ-03 |
| TASK-06 | Instrumentar logs/métricas para negações de acesso. | TASK-05 | NFR-03 |
| TASK-07 | Ativar feature flag e adicionar testes unitários/integrados cobrindo fluxos críticos. | TASK-05 | REQ-04, REQ-05 |

# 13. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | Migrações criadas e aplicáveis adicionando tabelas de roles/permissões e relacionamentos, com testes cobrindo persistência. |
| REQ-02 | Serviço permite atribuir/remover roles respeitando BR-01..BR-04 e atualiza `SecurityStamp` quando necessário. |
| REQ-03 | Endpoints CRUD respondem conforme contrato e retornam 403 quando usuário não possui permissão. |
| REQ-04 | Tokens emitidos incluem roles/permissões e são invalidados ao alterar `SecurityStamp`. |
| REQ-05 | Middleware bloqueia acesso quando permissão não casar com requisito declarado e loga tentativa. |
| REQ-06 | Seeds criam roles/permissões iniciais e podem ser personalizados via configuração. |

# 14. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-01 | Testar invariantes de criação de Role e Permission. |
| TEST-02 | Unit | REQ-02 | Validar atribuição e revogação de roles em `UserRoleService`. |
| TEST-03 | Integration | REQ-03 | Exercitar endpoints CRUD em ambiente in-memory com autenticação mock. |
| TEST-04 | Integration | REQ-04, REQ-05 | Simular fluxo de login, emissão de token, acesso autorizado e bloqueado. |
| TEST-05 | Unit | NFR-03 | Garantir logging estruturado ao negar permissão. |

# 15. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Estrutura de permissões | Utilizar strings em formato `dominio:recurso:acao` armazenadas em tabela dedicada. | Permissões codificadas em enums; claims dinâmicos por endpoint. | Facilita criação dinâmica e delega governança para base de dados. |
| DEC-02 | Invalidação de tokens | Atualizar `SecurityStamp` em `Role` e `User` para forçar refresh após mudanças críticas. | Não invalidar tokens até expiração natural. | Reduz janela de exposição após alteração de acesso. |
| DEC-03 | Gestão de Permissions | Permitir criação dinâmica via API (API-06, API-07, API-08) além de seeds iniciais. | Permissions apenas via seed/migrations (fixas). | Flexibilidade para adicionar novas permissões sem deploy; requer validação rigorosa para evitar duplicação/inconsistência. |
| DEC-04 | Organização de rotas | Agrupar endpoints RBAC por domínio (`/roles`, `/permissions`, `/users/{userId}/roles`) em vez de subgrupo único (`/auth/*`). | Concentrar tudo em `/auth/roles`, `/auth/permissions`, `/auth/users/{userId}/roles`. | Melhora separação de responsabilidades; facilita versionamento independente; alinha com padrão RESTful de recursos; simplifica documentação Scalar com tags separadas. |

# 16. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | Precisamos de granularidade por tenant/organização para roles? Resposta: Não há necessidade no momento; usuários recém-registrados iniciam sem roles e recebem acesso mínimo padrão. | Produto | Fechado |
| QST-02 | Usuários externos (clientes) usarão o mesmo conjunto de roles? Resposta: Usuários externos são criados sem roles e mantêm acesso mínimo; avaliaremos solução diferenciada apenas se surgir necessidade futura. | Produto | Fechado |

# 17. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0011-FEAT-role-based-access-control cumprindo REQ-01..REQ-06, criando ENT-01..ENT-04, endpoints API-01..API-10 restritos por permissões via middleware dedicado, atualizando emissão de JWT para incluir roles/permissões, aplicando feature flag `rbac-enabled` (FLAG-01 definido em SPEC-0006) e atendendo NFR-01..NFR-03. Permitir gestão dinâmica de permissions via API (DEC-03). Não introduzir modelos além dos listados e respeitar regras BR-01..BR-04.
