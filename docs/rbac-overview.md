# RBAC Overview

> Última revisão: 30/09/2025

Este documento resume os principais conceitos, rotas e permissões introduzidos pela implementação da especificação `SPEC-0011-FEAT-role-based-access-control`.

## Conceitos-chave

- **Role**: agrupamento de permissões associado a uma responsabilidade específica. Roles podem ser marcadas como de sistema (`IsSystemRole`) para impedir exclusões acidentais e garantir políticas obrigatórias.
- **Permission**: string única no formato `dominio:recurso:acao` que representa uma capacidade específica do usuário.
- **UserRole**: vínculo entre usuário e role. Cada atribuição registra quem executou a operação e quando ela ocorreu.
- **Security Stamp**: tokens JWT carregam carimbos (`SecurityStamp`) de usuário e roles; qualquer alteração relevante invalida tokens emitidos anteriormente.

## Endpoints disponíveis

**Nota:** Conforme DEC-04 do SPEC-0011, endpoints RBAC são organizados por domínio de recurso sob o grupo `/auth`.

| Rota | Verbo | Descrição | Permissão exigida |
|------|-------|-----------|-------------------|
| `/auth/roles` | GET | Lista todas as roles registradas. | `rbac:role:read` |
| `/auth/roles` | POST | Cria uma nova role com permissões associadas. | `rbac:role:create` |
| `/auth/roles/{roleId}` | PATCH | Atualiza metadados e conjunto de permissões de uma role existente. | `rbac:role:update` |
| `/auth/roles/{roleId}` | DELETE | Remove uma role (exceto roles de sistema). | `rbac:role:delete` |
| `/auth/permissions` | GET | Lista todas as permissões disponíveis. | `rbac:permission:read` |
| `/auth/permissions` | POST | Cria uma nova permissão. | `rbac:permission:create` |
| `/auth/permissions/{permissionId}` | PATCH | Atualiza nome exibido e descrição de uma permissão. | `rbac:permission:update` |
| `/auth/permissions/{permissionId}` | DELETE | Remove uma permissão existente. | `rbac:permission:delete` |
| `/auth/users/{userId}/roles` | POST | Atribui uma role a um usuário. | `rbac:user:role:assign` |
| `/auth/users/{userId}/roles/{roleId}` | DELETE | Revoga uma role de um usuário. | `rbac:user:role:revoke` |

> Todas as rotas exigem autenticação JWT válida. O filtro de permissão verifica as claims do token; tokens com carimbo inválido são rejeitados na validação.

## Payloads principais

### `RoleDto`

```json
{
  "id": "uuid",
  "name": "string",
  "displayName": "string",
  "description": "string | null",
  "isSystemRole": true,
  "securityStamp": "string",
  "createdAt": "2025-09-30T15:05:00Z",
  "updatedAt": "2025-09-30T15:05:00Z",
  "permissions": [
    {
      "id": "uuid",
      "name": "dominio:recurso:acao",
      "displayName": "string",
      "description": "string | null",
      "createdAt": "2025-09-30T15:05:00Z"
    }
  ]
}
```

### `PermissionDto`

```json
{
  "id": "uuid",
  "name": "dominio:recurso:acao",
  "displayName": "string",
  "description": "string | null",
  "createdAt": "2025-09-30T15:05:00Z"
}
```

### Criar role (`POST /auth/roles`)

```json
{
  "name": "admin",
  "displayName": "Administrator",
  "description": "Acesso total ao sistema.",
  "isSystemRole": true,
  "permissions": [
    "rbac:role:read",
    "rbac:role:create",
    "rbac:role:update",
    "rbac:role:delete"
  ]
}
```

### Atualizar role (`PATCH /auth/roles/{roleId}`)

```json
{
  "displayName": "Administrator",
  "description": "Acesso total ao sistema.",
  "permissions": [
    "rbac:role:read",
    "rbac:role:create",
    "rbac:role:update",
    "rbac:role:delete"
  ]
}
```

### Criar permissão (`POST /auth/permissions`)

```json
{
  "name": "analytics:dashboard:view",
  "displayName": "View analytics dashboard",
  "description": "Permite visualizar o dashboard analítico."
}
```

### Atualizar permissão (`PATCH /auth/permissions/{permissionId}`)

```json
{
  "displayName": "View analytics dashboard",
  "description": "Permite visualizar o dashboard analítico."
}
```

### Atribuir role a usuário (`POST /auth/users/{userId}/roles`)

```json
{
  "roleId": "uuid"
}
```

## Considerações de uso

1. Habilite o feature flag `rbac-enabled` nas configurações para ativar a validação granular.
   - Atualmente gerenciado via `Rbac:FeatureEnabled` em `appsettings.json`
   - Futuramente será migrado para o sistema de feature flags centralizado (SPEC-0006)
2. Seeds padrão podem ser configurados via `Rbac:Seed` em `appsettings.*`; roles de sistema comuns incluem `admin`, `manager` e `viewer`.
3. Ao alterar permissões de uma role ou atribuições de usuário, tokens existentes são invalidados automaticamente e novos tokens devem ser obtidos.

Para detalhes completos consulte o arquivo `specs/SPEC-0011-FEAT-role-based-access-control.md`.
