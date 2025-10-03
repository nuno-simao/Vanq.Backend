# Vanq CLI - Roadmap de Evolução (v0.1.0 → v1.0)

**Documento:** Roadmap de Implementação Completa
**Versão Atual:** v0.1.0 (MVP implementado)
**Versão Alvo:** v1.0 (SPEC-0012 100% conforme)
**Data:** 2025-10-03
**Responsável:** Equipe de Desenvolvimento

---

## 📊 Status Atual (v0.1.0)

### ✅ Implementado (92% conforme)

**Infraestrutura Base:**
- ✅ Projeto .NET 10 como .NET Global Tool
- ✅ Criptografia cross-platform (DPAPI/AES-256)
- ✅ Gerenciamento de configuração e credenciais
- ✅ HttpClient com retry logic e refresh automático
- ✅ Telemetria anônima com opt-out
- ✅ Formatação de output (JSON, Table, CSV)
- ✅ BaseCommand com tracking e error handling
- ✅ Documentação completa (README.md)

**Comandos Implementados (14/35):**
- ✅ Auth: login, logout, whoami (3/3)
- ✅ Config: list, add-profile, set-profile, telemetry (4/4)
- ✅ Role: list, create (2/7)
- ✅ Permission: - (0/5)
- ✅ User: - (0/5)
- ✅ Feature Flag: list, set (2/6)
- ✅ System Param: list, get (2/4)
- ✅ Health: health (1/1)

**Endpoints API Criados:**
- ✅ GET /health/ready
- ✅ POST /api/telemetry/cli

### ⚠️ Pendente (8% restante)

**Comandos Faltantes (21/35):**
- ⚠️ Role: get, update, delete, add-permission, remove-permission (5 comandos)
- ⚠️ Permission: list, get, create, update, delete (5 comandos)
- ⚠️ User: list, get, create, assign-role, revoke-role (5 comandos)
- ⚠️ Feature Flag: get, create, delete, audit (4 comandos)
- ⚠️ System Param: set, delete (2 comandos)

**Bugs Conhecidos:**
- ⚠️ Telemetry consent quebra em modo não-interativo
- ⚠️ CSV output não implementado (OutputFormatter existe mas sem implementação real)

**Melhorias de UX:**
- ⚠️ Cache de metadados (roles, permissions)
- ⚠️ Paginação para comandos list com muitos resultados
- ⚠️ Validação de SSL/TLS configurável (--insecure flag)
- ⚠️ Log rotation em ~/.vanq/logs/

---

## 🎯 Roadmap de Implementação

### **FASE 1: Comandos Críticos MUST** (Prioridade: 🔴 ALTA)

**Meta:** Completar 100% dos requisitos MUST da SPEC-0012

**Duração Estimada:** 3-5 dias

#### 1.1. Completar Comandos de Roles (REQ-06)

| Comando | Endpoint | Permissão | Estimativa | Status |
|---------|----------|-----------|------------|--------|
| `role get <roleId>` | GET /auth/roles/{id} | rbac:role:read | 1h | 📋 TODO |
| `role update <roleId>` | PATCH /auth/roles/{id} | rbac:role:update | 2h | 📋 TODO |
| `role delete <roleId>` | DELETE /auth/roles/{id} | rbac:role:delete | 1h | 📋 TODO |
| `role add-permission <roleId> <permissionName>` | PATCH /auth/roles/{id} | rbac:role:update | 2h | 📋 TODO |
| `role remove-permission <roleId> <permissionName>` | PATCH /auth/roles/{id} | rbac:role:update | 2h | 📋 TODO |

**Arquivos a criar:**
- `tools/Vanq.CLI/Commands/Role/RoleGetCommand.cs`
- `tools/Vanq.CLI/Commands/Role/RoleUpdateCommand.cs`
- `tools/Vanq.CLI/Commands/Role/RoleDeleteCommand.cs`
- `tools/Vanq.CLI/Commands/Role/RoleAddPermissionCommand.cs`
- `tools/Vanq.CLI/Commands/Role/RoleRemovePermissionCommand.cs`

**Exemplo de implementação:**
```csharp
// RoleGetCommand.cs
public static Command CreateCommand()
{
    var command = new Command("get", "Get role details by ID");

    var roleIdArg = new Argument<Guid>("roleId", "Role ID");
    command.AddArgument(roleIdArg);

    command.SetHandler(async (context) =>
    {
        var globalOpts = GlobalOptionsHelper.ExtractFromContext(context);
        var roleId = context.ParseResult.GetValueForArgument(roleIdArg);

        var cmd = new RoleGetCommand();
        await cmd.InitializeAsync(globalOpts);
        return await cmd.ExecuteWithTrackingAsync(
            "role.get",
            async () => await cmd.ExecuteAsync(roleId, globalOpts)
        );
    });

    return command;
}
```

**Total:** 8 horas

---

#### 1.2. Implementar Comandos de Permissions (REQ-07)

| Comando | Endpoint | Permissão | Estimativa | Status |
|---------|----------|-----------|------------|--------|
| `permission list` | GET /auth/permissions | rbac:permission:read | 1h | 📋 TODO |
| `permission get <permissionId>` | GET /auth/permissions/{id} | rbac:permission:read | 1h | 📋 TODO |
| `permission create <name> <displayName>` | POST /auth/permissions | rbac:permission:create | 2h | 📋 TODO |
| `permission update <permissionId>` | PATCH /auth/permissions/{id} | rbac:permission:update | 2h | 📋 TODO |
| `permission delete <permissionId>` | DELETE /auth/permissions/{id} | rbac:permission:delete | 1h | 📋 TODO |

**Arquivos a criar:**
- `tools/Vanq.CLI/Commands/Permission/PermissionListCommand.cs`
- `tools/Vanq.CLI/Commands/Permission/PermissionGetCommand.cs`
- `tools/Vanq.CLI/Commands/Permission/PermissionCreateCommand.cs`
- `tools/Vanq.CLI/Commands/Permission/PermissionUpdateCommand.cs`
- `tools/Vanq.CLI/Commands/Permission/PermissionDeleteCommand.cs`

**Registrar em Program.cs:**
```csharp
var permissionCommand = new Command("permission", "Permission management");
permissionCommand.AddCommand(PermissionListCommand.CreateCommand());
permissionCommand.AddCommand(PermissionGetCommand.CreateCommand());
permissionCommand.AddCommand(PermissionCreateCommand.CreateCommand());
permissionCommand.AddCommand(PermissionUpdateCommand.CreateCommand());
permissionCommand.AddCommand(PermissionDeleteCommand.CreateCommand());
rootCommand.AddCommand(permissionCommand);
```

**Total:** 7 horas

---

#### 1.3. Corrigir Bug de Telemetry Consent (NFR-05)

**Problema:** CLI quebra em modo não-interativo quando tenta perguntar consent de telemetria.

**Solução:**
```csharp
// Program.cs - CheckTelemetryConsentAsync()
private static async Task CheckTelemetryConsentAsync()
{
    var config = await ConfigManager.LoadConfigAsync();

    if (config.Telemetry?.ConsentGiven == null)
    {
        // Detectar modo não-interativo
        if (Console.IsInputRedirected || !Environment.UserInteractive)
        {
            // Modo não-interativo: desabilitar telemetria por padrão
            config.Telemetry ??= new TelemetrySettings();
            config.Telemetry.Enabled = false;
            config.Telemetry.ConsentGiven = false;
            config.Telemetry.ConsentDate = DateTime.UtcNow;
            await ConfigManager.SaveConfigAsync(config);
            return;
        }

        // Modo interativo: perguntar
        AnsiConsole.MarkupLine("[yellow]Welcome to Vanq CLI![/]");
        // ... resto do código de consent ...
    }
}
```

**Arquivo a modificar:**
- `tools/Vanq.CLI/Program.cs` (método `CheckTelemetryConsentAsync`)

**Total:** 1 hora

---

**Total FASE 1:** 16 horas (~2 dias úteis)

---

### **FASE 2: Comandos Importantes SHOULD** (Prioridade: 🟡 MÉDIA)

**Meta:** Completar gestão de usuários e feature flags avançados

**Duração Estimada:** 4-6 dias

#### 2.1. Implementar Comandos de Users (REQ-15)

| Comando | Endpoint | Permissão | Estimativa | Status |
|---------|----------|-----------|------------|--------|
| `user list` | GET /auth/users* | rbac:user:read | 2h | 📋 TODO |
| `user get <userId>` | GET /auth/users/{id}* | rbac:user:read | 1h | 📋 TODO |
| `user create <email> <password>` | POST /auth/register | Nenhuma | 2h | 📋 TODO |
| `user assign-role <userId> <roleId>` | POST /auth/users/{userId}/roles | rbac:user:role:assign | 2h | 📋 TODO |
| `user revoke-role <userId> <roleId>` | DELETE /auth/users/{userId}/roles/{roleId} | rbac:user:role:revoke | 2h | 📋 TODO |

**Nota:** *Endpoints de listagem e get de users NÃO existem na API. Precisam ser criados!

**API Endpoints a criar:**
```csharp
// Vanq.API/Endpoints/UserEndpoints.cs (NOVO)
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder apiRoute)
    {
        var group = apiRoute.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/", GetUsersAsync)
            .RequirePermission("rbac:user:read");

        group.MapGet("/{userId:guid}", GetUserByIdAsync)
            .RequirePermission("rbac:user:read");

        return group;
    }

    private static async Task<IResult> GetUsersAsync(
        IUserRepository userRepository,
        CancellationToken ct)
    {
        var users = await userRepository.GetAllAsync(ct);
        var dtos = users.Select(u => new UserDto(
            u.Id,
            u.Email,
            u.IsActive,
            u.CreatedAt,
            u.Roles.Select(ur => ur.Role.Name).ToList()
        ));
        return Results.Ok(dtos);
    }

    private static async Task<IResult> GetUserByIdAsync(
        Guid userId,
        IUserRepository userRepository,
        CancellationToken ct)
    {
        var user = await userRepository.GetByIdWithRolesAsync(userId, ct);
        if (user == null)
            return Results.NotFound();

        var dto = new UserDto(
            user.Id,
            user.Email,
            user.IsActive,
            user.CreatedAt,
            user.Roles.Select(ur => ur.Role.Name).ToList()
        );
        return Results.Ok(dto);
    }
}

public record UserDto(
    Guid Id,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    List<string> Roles
);
```

**Arquivos a criar:**
- API: `Vanq.API/Endpoints/UserEndpoints.cs` (novo)
- CLI: `tools/Vanq.CLI/Commands/User/UserListCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/User/UserGetCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/User/UserCreateCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/User/UserAssignRoleCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/User/UserRevokeRoleCommand.cs`

**Modificar:**
- `Vanq.API/Endpoints/Enpoints.cs` - registrar `MapUserEndpoints()`
- `Vanq.Application/Abstractions/Persistence/IUserRepository.cs` - adicionar `GetAllAsync()`

**Total:** 12 horas + 3 horas (API) = 15 horas

---

#### 2.2. Completar Comandos de Feature Flags (REQ-08)

| Comando | Endpoint | Permissão | Estimativa | Status |
|---------|----------|-----------|------------|--------|
| `feature-flag get <key>` | GET /api/admin/feature-flags/{key} | system:feature-flags:read | 1h | 📋 TODO |
| `feature-flag create <key> <environment> <value>` | POST /api/admin/feature-flags | system:feature-flags:write | 2h | 📋 TODO |
| `feature-flag delete <key>` | DELETE /api/admin/feature-flags/{key} | system:feature-flags:write | 1h | 📋 TODO |
| `feature-flag audit <key>` | GET /api/admin/feature-flags/{key}/audit | system:feature-flags:read | 2h | 📋 TODO |

**Nota:** Endpoint de audit (`/audit`) precisa ser implementado na API (SPEC-0006 v0.2.0).

**API Endpoint a criar:**
```csharp
// Vanq.API/Endpoints/FeatureFlagsEndpoints.cs - adicionar
group.MapGet("/{key}/audit", GetAuditHistoryAsync)
    .WithSummary("Get feature flag audit history")
    .RequirePermission("system:feature-flags:read");

private static async Task<IResult> GetAuditHistoryAsync(
    string key,
    IFeatureFlagAuditRepository auditRepository,
    CancellationToken ct)
{
    var history = await auditRepository.GetByKeyAsync(key, ct);
    return Results.Ok(history);
}
```

**Arquivos a criar:**
- CLI: `tools/Vanq.CLI/Commands/FeatureFlag/FeatureFlagGetCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/FeatureFlag/FeatureFlagCreateCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/FeatureFlag/FeatureFlagDeleteCommand.cs`
- CLI: `tools/Vanq.CLI/Commands/FeatureFlag/FeatureFlagAuditCommand.cs`

**Modificar:**
- API: `Vanq.API/Endpoints/FeatureFlagsEndpoints.cs` (adicionar endpoint /audit)

**Total:** 6 horas + 2 horas (API audit) = 8 horas

---

#### 2.3. Completar Comandos de System Parameters (REQ-09)

| Comando | Endpoint | Permissão | Estimativa | Status |
|---------|----------|-----------|------------|--------|
| `system-param set <key> <value> --type <type> --reason <reason>` | PUT /api/admin/system-params/{key} | system:params:write | 3h | 📋 TODO |
| `system-param delete <key>` | DELETE /api/admin/system-params/{key} | system:params:write | 1h | 📋 TODO |

**Arquivos a criar:**
- `tools/Vanq.CLI/Commands/SystemParam/SystemParamSetCommand.cs`
- `tools/Vanq.CLI/Commands/SystemParam/SystemParamDeleteCommand.cs`

**Total:** 4 horas

---

**Total FASE 2:** 27 horas (~3.5 dias úteis)

---

### **FASE 3: Melhorias de UX e Performance** (Prioridade: 🟢 BAIXA)

**Meta:** Melhorar experiência do usuário e performance

**Duração Estimada:** 3-4 dias

#### 3.1. Implementar Cache de Metadados (Performance)

**Objetivo:** Reduzir chamadas à API para listas de roles/permissions que mudam raramente.

**Implementação:**
```csharp
// tools/Vanq.CLI/Services/MetadataCache.cs (NOVO)
public class MetadataCache
{
    private readonly Dictionary<string, (object Data, DateTime ExpiresAt)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public T? Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow < entry.ExpiresAt)
                return entry.Data as T;

            _cache.Remove(key);
        }
        return null;
    }

    public void Set<T>(string key, T data) where T : class
    {
        _cache[key] = (data, DateTime.UtcNow.Add(_ttl));
    }

    public void Invalidate(string key) => _cache.Remove(key);
    public void Clear() => _cache.Clear();
}
```

**Modificar:**
- `BaseCommand.cs` - adicionar `protected MetadataCache Cache { get; private set; }`
- `RoleListCommand.cs`, `PermissionListCommand.cs` - usar cache

**Arquivos a criar:**
- `tools/Vanq.CLI/Services/MetadataCache.cs`

**Total:** 4 horas

---

#### 3.2. Implementar Paginação para Comandos List

**Objetivo:** Suportar `--page` e `--page-size` em comandos de listagem.

**Implementação:**
```csharp
// BaseCommand.cs - adicionar propriedades
protected int? Page { get; private set; }
protected int? PageSize { get; private set; }

// GlobalOptionsHelper.cs - adicionar opções globais
public static Option<int?> PageOption = new(
    aliases: new[] { "--page", "-p" },
    description: "Page number (1-based)"
);

public static Option<int?> PageSizeOption = new(
    aliases: new[] { "--page-size", "-ps" },
    description: "Items per page (default: 50, max: 200)"
);
```

**Modificar:**
- `GlobalOptionsHelper.cs` - adicionar opções de paginação
- `BaseCommand.cs` - adicionar propriedades Page/PageSize
- `RoleListCommand`, `PermissionListCommand`, `UserListCommand` - suportar paginação

**Total:** 6 horas

---

#### 3.3. Implementar Validação de SSL/TLS Configurável

**Objetivo:** Adicionar flag `--insecure` para desenvolvimento local com certificados auto-assinados.

**Implementação:**
```csharp
// GlobalOptionsHelper.cs
public static Option<bool> InsecureOption = new(
    aliases: new[] { "--insecure" },
    description: "Skip SSL certificate validation (dev only)"
);

// VanqApiClient.cs - modificar construtor
public VanqApiClient(string apiEndpoint, string profileName, bool insecure = false)
{
    var handler = new HttpClientHandler();

    if (insecure)
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    _httpClient = new HttpClient(handler)
    {
        BaseAddress = new Uri(apiEndpoint),
        Timeout = TimeSpan.FromSeconds(30)
    };
    _profileName = profileName;
}
```

**Modificar:**
- `GlobalOptionsHelper.cs` - adicionar `InsecureOption`
- `VanqApiClient.cs` - suportar parâmetro `insecure`
- `BaseCommand.cs` - passar flag para VanqApiClient

**Total:** 2 horas

---

#### 3.4. Implementar Log Rotation

**Objetivo:** Rotacionar logs em `~/.vanq/logs/vanq-cli.log` quando exceder 10MB.

**Implementação:**
```csharp
// tools/Vanq.CLI/Utilities/LogRotation.cs (NOVO)
public static class LogRotation
{
    private const long MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB

    public static void RotateIfNeeded(string logPath)
    {
        if (!File.Exists(logPath))
            return;

        var fileInfo = new FileInfo(logPath);
        if (fileInfo.Length < MaxLogSizeBytes)
            return;

        // Rotate: vanq-cli.log -> vanq-cli.log.1
        var backupPath = $"{logPath}.{DateTime.UtcNow:yyyyMMddHHmmss}";
        File.Move(logPath, backupPath);

        // Keep only last 5 rotated logs
        var dir = Path.GetDirectoryName(logPath)!;
        var rotatedLogs = Directory.GetFiles(dir, "vanq-cli.log.*")
            .OrderByDescending(f => f)
            .Skip(5);

        foreach (var oldLog in rotatedLogs)
            File.Delete(oldLog);
    }
}
```

**Modificar:**
- `BaseCommand.cs` - chamar `LogRotation.RotateIfNeeded()` antes de logar

**Arquivos a criar:**
- `tools/Vanq.CLI/Utilities/LogRotation.cs`

**Total:** 3 horas

---

#### 3.5. Implementar CSV Output Real

**Objetivo:** Atualmente `CsvOutputFormatter` existe mas não é usado corretamente.

**Modificar:**
- `BaseCommand.cs` - método `DisplayCsv<T>()` usar `CsvOutputFormatter`
- Testar com todos os comandos `list`

**Total:** 2 horas

---

#### 3.6. Adicionar Autocompletion para Shells

**Objetivo:** Gerar scripts de autocompletion para bash, zsh, powershell.

**Implementação:**
```bash
# System.CommandLine suporta geração automática
vanq completion bash > /etc/bash_completion.d/vanq
vanq completion zsh > /usr/share/zsh/site-functions/_vanq
vanq completion powershell > $PROFILE
```

**Modificar:**
- `Program.cs` - adicionar suporte a `completion` subcommand

**Total:** 4 horas

---

**Total FASE 3:** 21 horas (~2.5 dias úteis)

---

### **FASE 4: Testes e Qualidade** (Prioridade: 🟡 MÉDIA)

**Meta:** Garantir qualidade e confiabilidade do CLI

**Duração Estimada:** 3-4 dias

#### 4.1. Criar Testes Unitários

**Componentes a testar:**

| Componente | Arquivo de Teste | Estimativa |
|------------|------------------|------------|
| CredentialEncryption | CredentialEncryptionTests.cs | 3h |
| ConfigManager | ConfigManagerTests.cs | 2h |
| CredentialsManager | CredentialsManagerTests.cs | 2h |
| VanqApiClient | VanqApiClientTests.cs | 4h |
| MetadataCache | MetadataCacheTests.cs | 2h |
| TelemetryService | TelemetryServiceTests.cs | 2h |

**Criar projeto de testes:**
```bash
cd tools
dotnet new xunit -n Vanq.CLI.Tests
cd Vanq.CLI.Tests
dotnet add reference ../Vanq.CLI/Vanq.CLI.csproj
dotnet add package Shouldly
dotnet add package Moq
```

**Estrutura:**
```
tools/Vanq.CLI.Tests/
├── Configuration/
│   ├── ConfigManagerTests.cs
│   └── CredentialsManagerTests.cs
├── Services/
│   ├── CredentialEncryptionTests.cs
│   ├── VanqApiClientTests.cs
│   └── MetadataCacheTests.cs
└── Telemetry/
    └── TelemetryServiceTests.cs
```

**Exemplo de teste:**
```csharp
// CredentialEncryptionTests.cs
public class CredentialEncryptionTests
{
    [Fact]
    public void Encrypt_ShouldDecryptSuccessfully()
    {
        // Arrange
        var credentials = new CliCredentials(
            "test-profile",
            "access-token",
            "refresh-token",
            DateTime.UtcNow.AddHours(1),
            "user@example.com"
        );

        // Act
        var encrypted = CredentialEncryption.Encrypt(credentials);
        var decrypted = CredentialEncryption.Decrypt<CliCredentials>(encrypted);

        // Assert
        decrypted.ShouldNotBeNull();
        decrypted.Profile.ShouldBe("test-profile");
        decrypted.Email.ShouldBe("user@example.com");
    }
}
```

**Total:** 15 horas

---

#### 4.2. Criar Testes de Integração

**Cenários a testar:**

| Cenário | Arquivo de Teste | Estimativa |
|---------|------------------|------------|
| Login → Whoami → Logout | AuthFlowTests.cs | 3h |
| Multi-profile switching | ProfileSwitchingTests.cs | 2h |
| Token refresh automático | TokenRefreshTests.cs | 3h |
| Retry logic com backoff | RetryLogicTests.cs | 2h |

**Requisitos:**
- API rodando em background (Testcontainers ou Docker Compose)
- Database PostgreSQL em memória ou container

**Total:** 10 horas

---

**Total FASE 4:** 25 horas (~3 dias úteis)

---

### **FASE 5: Publicação e Distribuição** (Prioridade: 🟢 BAIXA)

**Meta:** Publicar CLI como .NET Tool global no NuGet.org

**Duração Estimada:** 1-2 dias

#### 5.1. Configurar CI/CD para Build Automático

**Plataforma:** GitHub Actions

**Workflow:**
```yaml
# .github/workflows/cli-build.yml
name: Vanq CLI Build

on:
  push:
    branches: [ main ]
    paths:
      - 'tools/Vanq.CLI/**'
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'

    - name: Restore
      run: dotnet restore tools/Vanq.CLI

    - name: Build
      run: dotnet build tools/Vanq.CLI --no-restore

    - name: Test
      run: dotnet test tools/Vanq.CLI.Tests --no-build

    - name: Publish (Windows)
      if: matrix.os == 'windows-latest'
      run: dotnet publish tools/Vanq.CLI -c Release -r win-x64 --self-contained

    - name: Publish (Linux)
      if: matrix.os == 'ubuntu-latest'
      run: dotnet publish tools/Vanq.CLI -c Release -r linux-x64 --self-contained

    - name: Publish (macOS)
      if: matrix.os == 'macos-latest'
      run: dotnet publish tools/Vanq.CLI -c Release -r osx-x64 --self-contained

    - name: Upload artifacts
      uses: actions/upload-artifact@v3
      with:
        name: vanq-cli-${{ matrix.os }}
        path: tools/Vanq.CLI/bin/Release/net10.0/*/publish/
```

**Total:** 4 horas

---

#### 5.2. Publicar no NuGet.org

**Passos:**

1. Criar conta no NuGet.org
2. Gerar API key
3. Configurar segredos no GitHub
4. Adicionar step de publicação no workflow

**Workflow adicional:**
```yaml
# .github/workflows/cli-publish.yml
name: Publish Vanq CLI to NuGet

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'

    - name: Pack
      run: dotnet pack tools/Vanq.CLI -c Release -o ./packages

    - name: Publish to NuGet
      run: dotnet nuget push ./packages/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

**Total:** 2 horas

---

#### 5.3. Criar Instaladores Standalone

**Plataformas:**
- Windows: MSI installer (WiX Toolset)
- Linux: DEB package, RPM package
- macOS: PKG installer

**Alternativa:** Usar dotnet publish com runtime incluído

**Total:** 8 horas (opcional, baixa prioridade)

---

**Total FASE 5:** 6 horas (~1 dia útil) + 8 horas opcional

---

## 📅 Cronograma Consolidado

| Fase | Descrição | Duração | Dias Úteis | Status |
|------|-----------|---------|------------|--------|
| **FASE 1** | Comandos Críticos MUST | 16h | 2 dias | 📋 TODO |
| **FASE 2** | Comandos Importantes SHOULD | 27h | 3.5 dias | 📋 TODO |
| **FASE 3** | Melhorias de UX e Performance | 21h | 2.5 dias | 📋 TODO |
| **FASE 4** | Testes e Qualidade | 25h | 3 dias | 📋 TODO |
| **FASE 5** | Publicação e Distribuição | 6h | 1 dia | 📋 TODO |
| **TOTAL** | | **95h** | **~12 dias** | 0% |

**Estimativa conservadora:** 12-15 dias úteis (~3 semanas)

---

## 🎯 Critérios de Aceite para v1.0

### Funcionalidade
- [ ] 100% comandos da SPEC-0012 implementados (35/35)
- [ ] Todos os endpoints da API necessários criados
- [ ] Zero bugs conhecidos críticos
- [ ] Telemetry consent funciona em modo não-interativo

### Qualidade
- [ ] Cobertura de testes ≥ 80%
- [ ] Todos os testes passando (unit + integration)
- [ ] Zero warnings de compilação
- [ ] Documentação completa e atualizada

### Performance
- [ ] Comandos `list` respondem em < 2s (p95)
- [ ] Cache de metadados implementado
- [ ] Retry logic testado e funcional

### Distribuição
- [ ] Publicado no NuGet.org como .NET Global Tool
- [ ] CI/CD configurado e funcional
- [ ] Binários standalone disponíveis para Windows/Linux/macOS

---

## 📋 Checklist de Implementação

### FASE 1: Comandos Críticos 🔴
- [ ] `role get <roleId>`
- [ ] `role update <roleId>`
- [ ] `role delete <roleId>`
- [ ] `role add-permission <roleId> <permissionName>`
- [ ] `role remove-permission <roleId> <permissionName>`
- [ ] `permission list`
- [ ] `permission get <permissionId>`
- [ ] `permission create <name> <displayName>`
- [ ] `permission update <permissionId>`
- [ ] `permission delete <permissionId>`
- [ ] Corrigir bug de telemetry consent em modo não-interativo

### FASE 2: Comandos Importantes 🟡
- [ ] Criar endpoint `GET /auth/users` na API
- [ ] Criar endpoint `GET /auth/users/{id}` na API
- [ ] `user list`
- [ ] `user get <userId>`
- [ ] `user create <email> <password>`
- [ ] `user assign-role <userId> <roleId>`
- [ ] `user revoke-role <userId> <roleId>`
- [ ] `feature-flag get <key>`
- [ ] `feature-flag create <key> <environment> <value>`
- [ ] `feature-flag delete <key>`
- [ ] Criar endpoint `GET /api/admin/feature-flags/{key}/audit` na API
- [ ] `feature-flag audit <key>`
- [ ] `system-param set <key> <value> --type <type> --reason <reason>`
- [ ] `system-param delete <key>`

### FASE 3: Melhorias de UX 🟢
- [ ] Implementar MetadataCache
- [ ] Adicionar opções `--page` e `--page-size`
- [ ] Implementar flag `--insecure` para SSL
- [ ] Implementar log rotation
- [ ] Corrigir CSV output
- [ ] Adicionar autocompletion para shells

### FASE 4: Testes 🟡
- [ ] CredentialEncryptionTests
- [ ] ConfigManagerTests
- [ ] CredentialsManagerTests
- [ ] VanqApiClientTests
- [ ] MetadataCacheTests
- [ ] TelemetryServiceTests
- [ ] AuthFlowTests (integration)
- [ ] ProfileSwitchingTests (integration)
- [ ] TokenRefreshTests (integration)
- [ ] RetryLogicTests (integration)

### FASE 5: Publicação 🟢
- [ ] Configurar GitHub Actions build
- [ ] Configurar GitHub Actions publish
- [ ] Publicar no NuGet.org
- [ ] (Opcional) Criar instaladores standalone

---

## 🚀 Quick Start para Contribuidores

### Setup de Desenvolvimento

```bash
# Clone o repositório
git clone https://github.com/vanq/vanq-backend
cd vanq-backend

# Restaurar dependências
dotnet restore Vanq.Backend.slnx

# Build do CLI
cd tools/Vanq.CLI
dotnet build

# Run do CLI
dotnet run -- --help

# Rodar testes (quando implementados)
cd ../Vanq.CLI.Tests
dotnet test
```

### Implementar um Novo Comando

1. **Criar arquivo do comando** em `tools/Vanq.CLI/Commands/<Grupo>/`
2. **Seguir padrão dos comandos existentes** (ver `RoleListCommand.cs` como referência)
3. **Registrar no `Program.cs`**
4. **Adicionar help text e exemplos**
5. **Testar localmente**: `dotnet run -- <comando>`
6. **Criar testes** em `tools/Vanq.CLI.Tests/`
7. **Atualizar README.md** com novo comando

### Padrão de Commit

```
feat(cli): add user list command

- Implements REQ-15 from SPEC-0012
- Requires new API endpoint GET /auth/users
- Supports --page and --page-size flags
- Includes unit tests

BREAKING CHANGE: none
```

---

## 📊 Métricas de Progresso

### Comandos Implementados

| Grupo | Implementados | Total | Progresso |
|-------|---------------|-------|-----------|
| Auth | 3 | 3 | ✅ 100% |
| Config | 4 | 4 | ✅ 100% |
| Role | 2 | 7 | ⚠️ 29% |
| Permission | 0 | 5 | ❌ 0% |
| User | 0 | 5 | ❌ 0% |
| Feature Flag | 2 | 6 | ⚠️ 33% |
| System Param | 2 | 4 | ⚠️ 50% |
| Health | 1 | 1 | ✅ 100% |
| **TOTAL** | **14** | **35** | **40%** |

### Requisitos SPEC-0012

| Tipo | Implementados | Total | Progresso |
|------|---------------|-------|-----------|
| MUST | 6 | 8 | ⚠️ 75% |
| SHOULD | 6 | 7 | ⚠️ 86% |
| NFRs | 6 | 6 | ✅ 100% |
| **TOTAL** | **18** | **21** | **86%** |

---

## 🔗 Referências

- **SPEC Principal:** `specs/SPEC-0012-FEAT-cli-tool.md`
- **Relatório de Validação:** `docs/SPEC-0012-validation-report.md`
- **README do CLI:** `tools/Vanq.CLI/README.md`
- **CLAUDE.md:** Documentação do projeto

---

## 📝 Histórico de Revisões

| Versão | Data | Autor | Mudanças |
|--------|------|-------|----------|
| v1.0 | 2025-10-03 | Claude Code | Roadmap inicial |

---

**Documento vivo:** Este roadmap será atualizado conforme o progresso da implementação.

**Responsável pela atualização:** Tech Lead / Product Owner

**Última atualização:** 2025-10-03
