# Feature Flags

## Visão Geral

O módulo de Feature Flags permite habilitar/desabilitar funcionalidades dinamicamente via banco de dados, com cache em memória para performance e invalidação automática quando um flag for alterado.

## Características

- **Persistência em Banco**: Flags armazenados no PostgreSQL via EF Core
- **Cache em Memória**: Leituras rápidas com `IMemoryCache` (TTL padrão: 60s)
- **Invalidação Automática**: Cache limpo imediatamente após alterações
- **Multi-Ambiente**: Valores diferentes por ambiente (Development, Staging, Production)
- **API REST**: Endpoints administrativos protegidos para gerenciamento
- **Auditoria Básica**: Registra quem e quando alterou cada flag
- **Fallback Seguro**: Retorna `false` em caso de erro, sem quebrar a aplicação

## Arquitetura

```
┌─────────────┐
│  Endpoint   │ ← Chamada do usuário
└──────┬──────┘
       │
┌──────▼───────────┐
│FeatureFlagService│ ← Resolve ambiente atual
└──────┬───────────┘
       │
       ├─ Cache Hit? → Retorna valor
       │
       └─ Cache Miss ──┐
                       │
                  ┌────▼────────────┐
                  │   Repository    │ ← Consulta banco
                  └─────────────────┘
```

## Uso Básico

### 1. Verificar Flag em Código

```csharp
public class MyService
{
    private readonly IFeatureFlagService _featureFlags;

    public MyService(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags;
    }

    public async Task DoSomethingAsync(CancellationToken ct)
    {
        // Verificação simples
        if (await _featureFlags.IsEnabledAsync("my-feature", ct))
        {
            // Feature habilitada
        }

        // Com fallback customizado
        var isEnabled = await _featureFlags.GetFlagOrDefaultAsync(
            "experimental-feature", 
            defaultValue: false, 
            ct);
    }
}
```

### 2. Gerenciar Flags via API

#### Listar todos os flags (todos ambientes)

```http
GET /api/admin/feature-flags
Authorization: Bearer {token}
```

#### Listar flags do ambiente atual

```http
GET /api/admin/feature-flags/current
Authorization: Bearer {token}
```

#### Buscar flag específico

```http
GET /api/admin/feature-flags/{key}
Authorization: Bearer {token}
```

#### Criar novo flag

```http
POST /api/admin/feature-flags
Authorization: Bearer {token}
Content-Type: application/json

{
  "key": "my-new-feature",
  "environment": "Development",
  "isEnabled": true,
  "description": "Descrição da feature",
  "isCritical": false,
  "metadata": "{\"owner\": \"team-platform\"}"
}
```

#### Atualizar flag existente

```http
PUT /api/admin/feature-flags/{key}
Authorization: Bearer {token}
Content-Type: application/json

{
  "isEnabled": false,
  "description": "Feature temporariamente desabilitada",
  "metadata": "{\"reason\": \"bug #1234\"}"
}
```

#### Toggle (ligar/desligar)

```http
POST /api/admin/feature-flags/{key}/toggle
Authorization: Bearer {token}
```

#### Deletar flag

```http
DELETE /api/admin/feature-flags/{key}
Authorization: Bearer {token}
```

## Flags Pré-Cadastrados

O sistema vem com flags pré-cadastrados para todas as features planejadas:

### Infraestrutura (Críticos)

| Key | Development | Staging | Production | Descrição |
|-----|-------------|---------|------------|-----------|
| `feature-flags-enabled` | ✓ | ✓ | ✓ | Kill switch do próprio módulo |
| `rbac-enabled` | ✓ | ✓ | ✓ | Habilita sistema RBAC |

### Features Planejadas (SPEC-000X)

| Key | Development | Staging | Production | SPEC |
|-----|-------------|---------|------------|------|
| `user-registration-enabled` | ✓ | ✓ | ✓ | SPEC-0001 |
| `cors-relaxed` | ✓ | ✗ | ✗ | SPEC-0002 |
| `problem-details-enabled` | ✓ | ✓ | ✗ | SPEC-0003 |
| `health-checks-enabled` | ✓ | ✓ | ✓ | SPEC-0004 |
| `error-middleware-enabled` | ✓ | ✓ | ✗ | SPEC-0005 |
| `system-params-enabled` | ✓ | ✓ | ✗ | SPEC-0007 |
| `rate-limiting-enabled` | ✗ | ✓ | ✓ | SPEC-0008 |
| `structured-logging-enabled` | ✓ | ✓ | ✓ | SPEC-0009 |
| `metrics-enabled` | ✓ | ✓ | ✓ | SPEC-0010 |
| `metrics-detailed-auth` | ✓ | ✗ | ✗ | SPEC-0010 |

### Flags Futuros (V2+)

| Key | Development | Staging | Production | SPEC |
|-----|-------------|---------|------------|------|
| `feature-flags-audit-enabled` | ✗ | ✗ | ✗ | SPEC-0006-V2 |

## Migração de `IRbacFeatureManager`

### Código Legado (Deprecado)

```csharp
// ⚠️ OBSOLETO - Será removido na v2.0
public class MyService
{
    private readonly IRbacFeatureManager _rbacFeature;

    public MyService(IRbacFeatureManager rbacFeature)
    {
        _rbacFeature = rbacFeature;
    }

    public async Task DoSomethingAsync(CancellationToken ct)
    {
        if (_rbacFeature.IsEnabled)
        {
            // ...
        }
        
        await _rbacFeature.EnsureEnabledAsync(ct);
    }
}
```

### Código Novo (Recomendado)

```csharp
// ✅ NOVO - Use IFeatureFlagService
public class MyService
{
    private readonly IFeatureFlagService _featureFlags;

    public MyService(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags;
    }

    public async Task DoSomethingAsync(CancellationToken ct)
    {
        if (await _featureFlags.IsEnabledAsync("rbac-enabled", ct))
        {
            // ...
        }
        
        // Equivalente a EnsureEnabledAsync
        if (!await _featureFlags.IsEnabledAsync("rbac-enabled", ct))
        {
            throw new InvalidOperationException("RBAC feature is disabled.");
        }
    }
}
```

### Cronograma de Depreciação

- **v1.0** (atual): `IRbacFeatureManager` marcado como `[Obsolete]`, mas funcional via adapter
- **v1.5**: Avisos intensificados nos logs ao usar interface legada
- **v2.0**: Remoção completa de `IRbacFeatureManager`

## Regras de Negócio

### Convenções de Nomenclatura

- **Formato**: `kebab-case` obrigatório (ex: `user-registration-enabled`)
- **Início**: Deve começar com letra minúscula
- **Caracteres**: Apenas letras minúsculas, dígitos e hífens
- **Hífens**: Não consecutivos, não no final
- **Tamanho máximo**: 128 caracteres

### Flags Críticos

Flags marcados com `IsCritical = true`:
- Exigem confirmação dupla antes de alteração (em implementações futuras)
- Geram logs com nível `Warning` ao serem alterados
- Exemplos: `feature-flags-enabled`, `rbac-enabled`

### Resolução de Ambiente

O ambiente atual é resolvido via `IHostEnvironment.EnvironmentName`, que utiliza a variável `ASPNETCORE_ENVIRONMENT`:

```bash
# Development
ASPNETCORE_ENVIRONMENT=Development

# Staging
ASPNETCORE_ENVIRONMENT=Staging

# Production
ASPNETCORE_ENVIRONMENT=Production
```

## Performance & Cache

### Estratégia de Cache

1. **Cache Hit**: Retorna valor diretamente da memória (~O(1))
2. **Cache Miss**: Consulta banco, armazena no cache (TTL: 60s)
3. **Invalidação**: Atualização/Toggle remove entrada do cache imediatamente

### Métricas de Performance

- **Consulta (cache quente)**: < 1ms
- **Consulta (cache frio)**: < 10ms (NFR-01)
- **Invalidação**: < 1s para propagar (NFR-02)

### Fallback em Caso de Erro

Se o cache ou banco falharem:
- `IsEnabledAsync`: Retorna `false`
- `GetFlagOrDefaultAsync`: Retorna o `defaultValue` fornecido
- Log de erro é registrado, mas aplicação continua funcionando

## Segurança

### Autorização

Todos os endpoints de administração exigem:
- Autenticação JWT válida
- Permissão `system:feature-flags:read` (leitura)
- Permissão `system:feature-flags:create` (criação)
- Permissão `system:feature-flags:update` (atualização/toggle)
- Permissão `system:feature-flags:delete` (deleção)

### Auditoria

Todas as alterações registram:
- **Quem**: `LastUpdatedBy` (email do usuário autenticado)
- **Quando**: `LastUpdatedAt` (UTC timestamp)
- **O quê**: Log estruturado com `OldValue`, `NewValue`, `Key`, `Environment`

Exemplo de log:

```json
{
  "timestamp": "2025-10-01T12:34:56Z",
  "level": "Information",
  "message": "Feature flag updated",
  "properties": {
    "Key": "user-registration-enabled",
    "Environment": "Production",
    "OldValue": true,
    "NewValue": false,
    "UpdatedBy": "admin@vanq.com"
  }
}
```

## Observabilidade

### Logs Estruturados

O serviço emite logs para:
- Cache hit/miss
- Alterações de flags
- Erros de consulta/persistência

### Métricas (Planejado - SPEC-0010)

Quando `metrics-enabled` estiver ativo:
- `feature_flag_check_total{key, environment, cache_hit}`
- `feature_flag_toggle_total{key, environment, old_value, new_value}`
- `feature_flag_cache_duration_seconds{key}`

## Limitações & Futuro

### Não Suportado na V1

- ❌ Targeting por usuário/grupo
- ❌ Rollout progressivo (% de usuários)
- ❌ Scheduling de ativação/desativação
- ❌ Auditoria completa (histórico imutável) → Ver SPEC-0006-V2
- ❌ UI de administração web
- ❌ Integração com serviços externos (LaunchDarkly, Azure App Config)

### Roadmap V2 (SPEC-0006-V2)

- ✓ Tabela de auditoria completa (`FeatureFlagAuditLog`)
- ✓ Endpoints de consulta de histórico
- ✓ Exportação CSV/JSON para auditores externos
- ✓ Política de retenção configurável
- ✓ Importação de logs estruturados pré-existentes

## Solução de Problemas

### Flag não está sendo atualizado

**Problema**: Alterei o flag via API, mas o valor antigo ainda aparece.

**Solução**:
1. Verifique se o ambiente está correto (`ASPNETCORE_ENVIRONMENT`)
2. O cache é invalidado automaticamente, mas tem TTL de 60s
3. Reinicie a aplicação se o problema persistir

### Erro "Feature flag key must be in kebab-case format"

**Problema**: Tentei criar flag `MyFeature` e recebi erro.

**Solução**: Use formato `kebab-case`: `my-feature`

### Erro ao aplicar migration

**Problema**: `PendingModelChangesWarning` ao rodar `dotnet ef database update`.

**Solução**: A configuração usa valores determinísticos (SHA-256 do key+environment). Se ainda assim ocorrer, remova e recrie a migration.

## Exemplos Avançados

### Múltiplos Flags com Fallback

```csharp
public async Task<bool> IsFeatureAvailableAsync(CancellationToken ct)
{
    // Verifica flag master primeiro
    if (!await _featureFlags.IsEnabledAsync("feature-flags-enabled", ct))
    {
        return false;
    }

    // Verifica flag específico com fallback
    return await _featureFlags.GetFlagOrDefaultAsync(
        "my-experimental-feature", 
        defaultValue: false, 
        ct);
}
```

### Criar Flag Programaticamente

```csharp
var createDto = new CreateFeatureFlagDto(
    Key: "dynamic-feature",
    Environment: _environment.EnvironmentName,
    IsEnabled: false,
    Description: "Feature criada dinamicamente",
    IsCritical: false,
    Metadata: "{\"team\": \"platform\"}"
);

var flag = await _featureFlags.CreateAsync(
    createDto, 
    updatedBy: "system", 
    ct);
```

## Referências

- **SPEC**: [SPEC-0006-FEAT-feature-flags.md](../specs/SPEC-0006-FEAT-feature-flags.md)
- **SPEC V2**: [SPEC-0006-V2-FEAT-feature-flags-audit.md](../specs/SPEC-0006-V2-FEAT-feature-flags-audit.md)
- **Código**: `Vanq.Infrastructure/FeatureFlags/`

---

**Última atualização**: 2025-10-01  
**Versão**: 1.0
