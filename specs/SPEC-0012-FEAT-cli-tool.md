---
spec:
  id: SPEC-0012-FEAT-cli-tool
  type: feature
  version: 0.1.0
  status: draft
  owner: nuno-simao
  created: 2025-10-02
  updated: 2025-10-02
  priority: medium
  quality_order: [security, reliability, performance, observability, delivery_speed, cost]
  tags: [cli, devops, administration, tooling]
  depends_on: [SPEC-0006, SPEC-0007, SPEC-0011]
---

# 1. Objetivo
Fornecer uma ferramenta de linha de comando (CLI) chamada **Vanq** para administradores e desenvolvedores gerenciarem o backend Vanq.API de forma eficiente, permitindo operações de autenticação, CRUD de recursos (usuários, roles, permissions, feature flags, system parameters) e monitoramento básico, sem necessidade de interface gráfica ou ferramentas HTTP externas.

# 2. Escopo
## 2.1 In
- CLI multiplataforma (.NET Tool global ou executável standalone) chamado `vanq`.
- Sistema de autenticação seguro com armazenamento local criptografado de credenciais.
- Comandos para gestão de usuários (`user`), roles (`role`), permissions (`permission`), feature flags (`feature-flag`), system parameters (`system-param`).
- Comandos de monitoramento: health checks, métricas básicas, logs estruturados.
- Comandos administrativos: `login`, `logout`, `whoami`, `config`, `--version`, `--help`.
- Configuração de endpoint da API via arquivo de configuração ou variável de ambiente.
- Output formatado (JSON, Table, CSV) configurável via flags.
- Suporte a profiles para múltiplos ambientes (dev, staging, production).

## 2.2 Out
- Interface gráfica (GUI) ou TUI (Terminal User Interface) interativa.
- Funcionalidades de desenvolvimento local (scaffolding, code generation).
- Integração com pipelines CI/CD (foco inicial em uso manual).
- Auto-update do CLI (gerenciado via .NET Tool ou package manager).

## 2.3 Não Fazer
- Implementar lógica de negócio duplicada (CLI apenas consome API).
- Armazenar senhas em texto claro (sempre criptografadas).
- Expor endpoints não autenticados além de `--version` e `--help`.

# 3. Requisitos Funcionais
| ID | Descrição | Criticidade (MUST/SHOULD/MAY) |
|----|-----------|--------------------------------|
| REQ-01 | Permitir login com email/password armazenando tokens JWT de forma segura localmente. | MUST |
| REQ-02 | Carregar automaticamente credenciais armazenadas em execuções subsequentes. | MUST |
| REQ-03 | Implementar logout que remove credenciais locais e revoga refresh token no servidor. | MUST |
| REQ-04 | Suportar comandos `--version` e `--help` sem autenticação. | MUST |
| REQ-05 | Disponibilizar comando `whoami` para verificar usuário autenticado e permissões. | SHOULD |
| REQ-06 | Implementar comandos CRUD para roles (`role list`, `role create`, `role update`, `role delete`). | MUST |
| REQ-07 | Implementar comandos CRUD para permissions (`permission list`, `permission create`, etc.). | MUST |
| REQ-08 | Implementar comandos de gestão de feature flags (`feature-flag list`, `feature-flag set`, etc.). | MUST |
| REQ-09 | Implementar comandos de gestão de system parameters (`system-param get`, `system-param set`). | SHOULD |
| REQ-10 | Implementar comando de health check (`health`) para verificar status da API. | SHOULD |
| REQ-11 | Suportar output em múltiplos formatos (json, table, csv) via flag `--output`. | SHOULD |
| REQ-12 | Permitir configuração de múltiplos profiles (ambientes) via `config` command. | SHOULD |
| REQ-13 | Exibir mensagens de erro amigáveis com códigos HTTP e sugestões de correção. | SHOULD |
| REQ-14 | Implementar refresh automático de access token quando expirado. | MUST |
| REQ-15 | Suportar atribuição/revogação de roles a usuários (`user assign-role`, `user revoke-role`). | SHOULD |

# 4. Requisitos Não Funcionais (Prioridades Relevantes)
| ID | Categoria | Descrição | Métrica / Aceite |
|----|-----------|-----------|------------------|
| NFR-01 | Segurança | Credenciais armazenadas com criptografia usando DPAPI (Windows) ou equivalente cross-platform. | Auditoria de código |
| NFR-02 | Performance | Comandos devem responder em < 2s p95 para operações de leitura. | Medição local |
| NFR-03 | Usabilidade | Help text claro e exemplos para todos os comandos. | 100% comandos documentados |
| NFR-04 | Confiabilidade | Retry automático com backoff exponencial em caso de falha de rede (3 tentativas). | Testes de resiliência |
| NFR-05 | Observabilidade | Modo verbose (`--verbose`) que exibe detalhes de requisições HTTP e erros. | Flag implementada |
| NFR-06 | Portabilidade | Funcionar em Windows, Linux e macOS sem alterações. | Testes em 3 plataformas |

# 5. Regras de Negócio
| ID | Descrição |
|----|-----------|
| BR-01 | Tokens armazenados localmente devem expirar e serem renovados automaticamente via refresh token. |
| BR-02 | Comandos que modificam estado (create, update, delete) exigem confirmação interativa (--force para bypass). |
| BR-03 | Credenciais armazenadas são específicas por profile (ambiente). |
| BR-04 | Valores sensíveis (passwords, tokens) nunca devem aparecer em logs ou output padrão. |
| BR-05 | CLI deve validar permissões localmente antes de enviar requisição (fail-fast). |

# 6. Novas Entidades
Nenhuma entidade de domínio adicional no backend (CLI é cliente).

| ID | Nome | Propósito | Observações |
|----|------|-----------|-------------|
| ENT-01 | CliConfig | Armazenar configuração local do CLI (profiles, endpoint, preferências). | Arquivo JSON em `~/.vanq/config.json` |
| ENT-02 | CliCredentials | Armazenar tokens criptografados por profile. | Arquivo criptografado em `~/.vanq/credentials.bin` |

## 6.1 Campos (Arquivos de Configuração Local)
### CliConfig (`~/.vanq/config.json`)
| Campo | Tipo | Nullable | Regra / Constraint |
|-------|------|----------|--------------------|
| CurrentProfile | string | Não | Nome do profile ativo |
| Profiles | Profile[] | Não | Lista de profiles configurados |

### Profile
| Campo | Tipo | Nullable | Regra / Constraint |
|-------|------|----------|--------------------|
| Name | string | Não | Identificador único (dev, staging, prod) |
| ApiEndpoint | string (URL) | Não | URL base da API (ex.: https://api.vanq.io) |
| OutputFormat | string | Sim | Default: table. Valores: json, table, csv |

### CliCredentials (criptografado)
| Campo | Tipo | Nullable | Regra / Constraint |
|-------|------|----------|--------------------|
| Profile | string | Não | Nome do profile |
| AccessToken | string | Não | JWT access token |
| RefreshToken | string | Não | Refresh token |
| ExpiresAt | DateTime (UTC) | Não | Timestamp de expiração do access token |
| Email | string | Não | Email do usuário autenticado |

# 7. Impactos Arquiteturais
| Camada | Alterações | Notas |
|--------|------------|-------|
| Domain | Nenhuma (CLI é cliente externo). | |
| Application | Nenhuma. | |
| Infrastructure | Nenhuma. | |
| API | Nenhuma (CLI consome endpoints existentes). | |
| **Nova Camada** | **Vanq.CLI** (novo projeto) | Console Application usando System.CommandLine ou Spectre.Console |

# 8. Arquitetura do CLI
## 8.1 Tecnologias
- **.NET 10**: Console Application
- **System.CommandLine**: Framework para parsing de comandos e argumentos
- **Spectre.Console**: Formatação de output (tables, colors, prompts)
- **System.Security.Cryptography**: Criptografia de credenciais
- **System.Text.Json**: Serialização/deserialização JSON
- **HttpClient**: Comunicação com Vanq.API

## 8.2 Estrutura de Comandos
```
vanq --version                          # Exibe versão do CLI
vanq --help                             # Exibe ajuda geral
vanq login                              # Autentica usuário
vanq logout                             # Remove credenciais e revoga token
vanq whoami                             # Exibe usuário autenticado
vanq config list                        # Lista profiles
vanq config set-profile <name>          # Troca profile ativo
vanq config add-profile <name> <url>    # Adiciona novo profile
vanq health                             # Verifica status da API

# Gestão de Roles
vanq role list                          # Lista todas as roles
vanq role get <roleId>                  # Exibe detalhes de uma role
vanq role create <name> <displayName>   # Cria nova role
vanq role update <roleId>               # Atualiza role
vanq role delete <roleId>               # Remove role
vanq role add-permission <roleId> <permissionName>  # Adiciona permissão
vanq role remove-permission <roleId> <permissionName>

# Gestão de Permissions
vanq permission list                    # Lista todas as permissions
vanq permission get <permissionId>      # Exibe detalhes
vanq permission create <name> <displayName>
vanq permission update <permissionId>
vanq permission delete <permissionId>

# Gestão de Usuários
vanq user list                          # Lista usuários (paginado)
vanq user get <userId>                  # Exibe detalhes
vanq user assign-role <userId> <roleId> # Atribui role
vanq user revoke-role <userId> <roleId> # Remove role

# Gestão de Feature Flags
vanq feature-flag list                  # Lista flags do ambiente atual
vanq feature-flag list --all            # Lista flags de todos os ambientes
vanq feature-flag get <key>             # Exibe detalhes
vanq feature-flag set <key> <true|false> --reason "motivo"
vanq feature-flag create <key> <environment> <value>
vanq feature-flag delete <key>
vanq feature-flag audit <key>           # Exibe histórico de alterações (SPEC-0006-V2)

# Gestão de System Parameters
vanq system-param list                  # Lista todos os parâmetros
vanq system-param get <key>             # Obtém valor
vanq system-param set <key> <value> --type <string|int|bool|json> --reason "motivo"
vanq system-param delete <key>

# Flags Globais
--profile <name>                        # Override de profile
--output <json|table|csv>               # Formato de saída
--verbose                               # Modo detalhado
--force                                 # Bypass de confirmações
--no-color                              # Desabilita cores
```

# 9. API (Se aplicável)
CLI consome endpoints existentes da Vanq.API. Não adiciona novos endpoints.

## 9.1 Mapeamento de Comandos para Endpoints
| Comando CLI | Método HTTP | Endpoint API | Permissão Requerida |
|-------------|-------------|--------------|---------------------|
| `login` | POST | `/auth/login` | Nenhuma |
| `logout` | POST | `/auth/logout` | Autenticado |
| `whoami` | GET | `/auth/me` | Autenticado |
| `health` | GET | `/health/ready` | Nenhuma |
| `role list` | GET | `/auth/roles` | `rbac:role:read` |
| `role create` | POST | `/auth/roles` | `rbac:role:create` |
| `role update` | PATCH | `/auth/roles/{id}` | `rbac:role:update` |
| `role delete` | DELETE | `/auth/roles/{id}` | `rbac:role:delete` |
| `permission list` | GET | `/auth/permissions` | `rbac:permission:read` |
| `permission create` | POST | `/auth/permissions` | `rbac:permission:create` |
| `user assign-role` | POST | `/auth/users/{userId}/roles` | `rbac:user:role:assign` |
| `user revoke-role` | DELETE | `/auth/users/{userId}/roles/{roleId}` | `rbac:user:role:revoke` |
| `feature-flag list` | GET | `/api/admin/feature-flags/current` | `system:feature-flags:read` |
| `feature-flag set` | PATCH | `/api/admin/feature-flags/{key}` | `system:feature-flags:write` |
| `feature-flag audit` | GET | `/api/admin/feature-flags/{key}/audit` | `system:feature-flags:read` |
| `system-param list` | GET | `/admin/system-params` | Admin role |
| `system-param set` | PUT | `/admin/system-params/{key}` | Admin role |

# 10. Segurança & Performance
- **Segurança**:
  - Credenciais criptografadas com DPAPI (Windows) ou `System.Security.Cryptography.ProtectedData` com cross-platform fallback (AES-256 com chave derivada de machine key).
  - Tokens nunca logados ou exibidos no output.
  - Modo `--verbose` exibe apenas headers HTTP, não bodies com dados sensíveis.
  - Validação de certificados SSL/TLS habilitada por padrão (opção `--insecure` para dev).

- **Performance**:
  - Cache de metadados de API (lista de permissions/roles) por 5 minutos para reduzir chamadas.
  - Refresh de access token lazy (apenas quando detectar 401).
  - Connection pooling padrão do HttpClient.

- **Observabilidade**:
  - Logs opcionais em arquivo `~/.vanq/logs/vanq-cli.log` com rotação.
  - Métricas de uso enviadas anonimamente para telemetria (opt-out via config).
  - Exit codes padronizados: 0 (sucesso), 1 (erro genérico), 2 (autenticação falhou), 3 (permissão negada).

# 11. i18n
Não aplicável (mensagens técnicas em inglês). Considerar pt-BR em versão futura.

# 12. Feature Flags
Nenhum flag necessário (CLI é ferramenta standalone).

# 13. Tarefas
| ID | Descrição | Dependências | REQs |
|----|-----------|--------------|------|
| TASK-01 | Criar projeto Vanq.CLI e configurar System.CommandLine + Spectre.Console. | - | REQ-01 |
| TASK-02 | Implementar CliConfig e CliCredentials com criptografia cross-platform. | TASK-01 | REQ-01,REQ-02 |
| TASK-03 | Implementar comandos `login`, `logout`, `whoami` com armazenamento seguro. | TASK-02 | REQ-01,REQ-03,REQ-05 |
| TASK-04 | Implementar refresh automático de access token. | TASK-03 | REQ-14 |
| TASK-05 | Implementar comandos de roles (list, create, update, delete). | TASK-03 | REQ-06 |
| TASK-06 | Implementar comandos de permissions (list, create, update, delete). | TASK-03 | REQ-07 |
| TASK-07 | Implementar comandos de feature flags (list, set, audit). | TASK-03 | REQ-08 |
| TASK-08 | Implementar comandos de system parameters (get, set, list). | TASK-03 | REQ-09 |
| TASK-09 | Implementar comandos de usuários (list, assign-role, revoke-role). | TASK-03 | REQ-15 |
| TASK-10 | Implementar comando `health` para health checks. | TASK-03 | REQ-10 |
| TASK-11 | Implementar sistema de profiles e comando `config`. | TASK-02 | REQ-12 |
| TASK-12 | Implementar formatação de output (json, table, csv) via `--output`. | TASK-01 | REQ-11 |
| TASK-13 | Implementar retry logic com backoff exponencial. | TASK-03 | NFR-04 |
| TASK-14 | Implementar modo `--verbose` e logging em arquivo. | TASK-03 | NFR-05 |
| TASK-15 | Adicionar help text e exemplos para todos os comandos. | TASK-01..10 | NFR-03 |
| TASK-16 | Criar testes unitários para lógica de criptografia e configuração. | TASK-02 | NFR-01 |
| TASK-17 | Criar testes de integração para comandos principais. | TASK-03..10 | REQ-01..15 |
| TASK-18 | Configurar build para Windows, Linux e macOS. | TASK-01 | NFR-06 |
| TASK-19 | Documentar instalação e uso no README. | TASK-15 | REQ-04 |
| TASK-20 | Publicar como .NET Tool global no NuGet. | TASK-18 | - |

# 14. Critérios de Aceite
| REQ | Critério |
|-----|----------|
| REQ-01 | `vanq login` autentica usuário e armazena tokens criptografados localmente. |
| REQ-02 | Comando `vanq role list` executa sem nova autenticação após login bem-sucedido. |
| REQ-03 | `vanq logout` remove credenciais locais e revoga refresh token no servidor. |
| REQ-04 | `vanq --version` e `vanq --help` funcionam sem autenticação. |
| REQ-05 | `vanq whoami` exibe email, userId, roles e permissions do usuário autenticado. |
| REQ-06 | Comandos `role list/create/update/delete` funcionam e exigem permissões apropriadas. |
| REQ-07 | Comandos `permission list/create/update/delete` funcionam corretamente. |
| REQ-08 | Comandos `feature-flag list/set/audit` gerenciam flags corretamente. |
| REQ-09 | Comandos `system-param get/set` funcionam e validam tipos. |
| REQ-10 | `vanq health` retorna status Healthy/Unhealthy da API. |
| REQ-11 | Flag `--output json` retorna JSON válido; `--output table` exibe tabela formatada. |
| REQ-12 | `vanq config add-profile prod https://api.vanq.io` adiciona profile e permite troca com `set-profile`. |
| REQ-14 | Access token expirado é renovado automaticamente antes de falhar com 401. |
| REQ-15 | `vanq user assign-role <userId> <roleId>` atribui role e atualiza security stamp do usuário. |

# 15. Testes (Mapa Resumido)
| TEST | Tipo | Cobre REQ | Descrição |
|------|------|-----------|-----------|
| TEST-01 | Unit | REQ-01,REQ-02 | Verifica criptografia/descriptografia de credenciais. |
| TEST-02 | Unit | REQ-12 | Testa leitura/escrita de arquivo de configuração. |
| TEST-03 | Integration | REQ-01,REQ-03 | Fluxo completo login → comando autenticado → logout. |
| TEST-04 | Integration | REQ-06 | Comandos de roles (CRUD completo). |
| TEST-05 | Integration | REQ-08 | Comandos de feature flags. |
| TEST-06 | Integration | REQ-14 | Refresh automático de token expirado. |
| TEST-07 | Unit | NFR-04 | Retry logic com backoff exponencial. |
| TEST-08 | Integration | REQ-11 | Formatação de output (json/table/csv). |
| TEST-09 | Integration | REQ-10 | Health check retorna status correto. |
| TEST-10 | Manual | NFR-06 | Executar CLI em Windows, Linux e macOS. |

# 16. Decisões
| ID | Contexto | Decisão | Alternativas | Consequência |
|----|----------|--------|--------------|--------------|
| DEC-01 | Framework de comandos | Usar System.CommandLine (Microsoft) | Spectre.Console.Cli, CommandLineParser | Melhor integração com .NET moderno e suporte oficial. |
| DEC-02 | Armazenamento de credenciais | DPAPI (Windows) + AES-256 com machine key (cross-platform) | Keychain/Keyring (OS-specific) | Simplicidade cross-platform sem dependências externas. |
| DEC-03 | Formato de configuração | JSON em `~/.vanq/config.json` | YAML, TOML | Consistência com ecossistema .NET. |
| DEC-04 | Distribuição | .NET Tool global via NuGet | Executável standalone, instalador MSI/DEB | Facilita instalação com `dotnet tool install -g vanq-cli`. |
| DEC-05 | Refresh de token | Lazy (apenas quando detectar 401) | Proativo (verificar expiração antes de cada request) | Reduz overhead de verificação constante. |
| DEC-06 | Confirmação de comandos destrutivos | Prompt interativo (bypass com --force) | Sempre executar sem confirmação | Previne erros acidentais em produção. |

# 17. Pendências / Questões
| ID | Pergunta | Responsável | Status |
|----|----------|-------------|--------|
| QST-01 | CLI deve suportar criação de usuários ou apenas gestão de roles/permissions? | owner | Aberto |
| QST-02 | Implementar telemetria anônima para métricas de uso? (opt-out) | owner | Aberto |
| QST-03 | Suportar arquivos de configuração `.env` para variáveis de ambiente? | owner | Aberto |
| QST-04 | Adicionar comando `vanq backup` para exportar configuração completa da API? | owner | Aberto |

# 18. Exemplos de Uso

## 18.1 Setup Inicial
```bash
# Instalar CLI
dotnet tool install -g vanq-cli

# Configurar profile de produção
vanq config add-profile prod https://api.vanq.io

# Fazer login
vanq login
# Email: admin@vanq.io
# Password: ********
# ✓ Login successful

# Verificar usuário autenticado
vanq whoami
# Email: admin@vanq.io
# Roles: admin, auditor
# Permissions: rbac:*, system:*, ...
```

## 18.2 Gestão de Roles
```bash
# Listar roles
vanq role list --output table
# ┌──────────────────────────────────────┬─────────┬──────────────┬──────────────┐
# │ ID                                   │ Name    │ Display Name │ System Role  │
# ├──────────────────────────────────────┼─────────┼──────────────┼──────────────┤
# │ 123e4567-e89b-12d3-a456-426614174000 │ admin   │ Administrator│ Yes          │
# │ 123e4567-e89b-12d3-a456-426614174001 │ viewer  │ Viewer       │ Yes          │
# └──────────────────────────────────────┴─────────┴──────────────┴──────────────┘

# Criar nova role
vanq role create moderator "Content Moderator" --description "Manages user content"
# ✓ Role 'moderator' created successfully

# Adicionar permissão
vanq role add-permission moderator content:post:delete
# ✓ Permission 'content:post:delete' added to role 'moderator'

# Atribuir role a usuário
vanq user assign-role 98765432-e89b-12d3-a456-426614174999 moderator
# ✓ Role 'moderator' assigned to user
```

## 18.3 Gestão de Feature Flags
```bash
# Listar flags do ambiente atual
vanq feature-flag list
# ┌─────────────────────┬─────────────┬─────────┬──────────────────────┐
# │ Key                 │ Environment │ Enabled │ Last Updated         │
# ├─────────────────────┼─────────────┼─────────┼──────────────────────┤
# │ rbac-enabled        │ Production  │ true    │ 2025-10-01 14:32:10  │
# │ new-feature-beta    │ Production  │ false   │ 2025-09-28 09:15:22  │
# └─────────────────────┴─────────────┴─────────┴──────────────────────┘

# Habilitar feature flag
vanq feature-flag set new-feature-beta true --reason "Gradual rollout to 10% users"
# ⚠ This will change feature flag in PRODUCTION environment
# Continue? [y/N]: y
# ✓ Feature flag 'new-feature-beta' set to true

# Ver histórico de auditoria
vanq feature-flag audit rbac-enabled --output json
```

## 18.4 Gestão de System Parameters
```bash
# Obter parâmetro
vanq system-param get auth.password.minLength
# Value: 8
# Type: int

# Atualizar parâmetro
vanq system-param set auth.password.minLength 12 --type int --reason "Security policy update"
# ✓ Parameter 'auth.password.minLength' updated to 12

# Listar todos os parâmetros
vanq system-param list --output csv
```

## 18.5 Health Check
```bash
# Verificar saúde da API
vanq health
# Status: Healthy
# Database: Healthy (15ms)
# Environment: Healthy
# Uptime: 5d 12h 34m
```

## 18.6 Múltiplos Ambientes
```bash
# Adicionar profile de staging
vanq config add-profile staging https://staging-api.vanq.io

# Trocar para staging
vanq config set-profile staging

# Fazer login em staging
vanq login

# Executar comando em staging
vanq feature-flag list

# Executar comando em produção sem trocar profile ativo
vanq role list --profile prod
```

# 19. Observabilidade e Debugging

## 19.1 Modo Verbose
```bash
vanq role list --verbose
# [DEBUG] Loading config from ~/.vanq/config.json
# [DEBUG] Current profile: prod
# [DEBUG] API Endpoint: https://api.vanq.io
# [DEBUG] Loading credentials for profile 'prod'
# [DEBUG] Access token expires at: 2025-10-02 15:30:00 UTC
# [DEBUG] HTTP GET https://api.vanq.io/auth/roles
# [DEBUG] Request Headers: Authorization: Bearer eyJ...
# [DEBUG] Response Status: 200 OK
# [DEBUG] Response Time: 123ms
# ┌──────────────────────────────────────┬─────────┬──────────────┐
# │ ID                                   │ Name    │ Display Name │
# └──────────────────────────────────────┴─────────┴──────────────┘
```

## 19.2 Logs Persistentes
Logs detalhados em `~/.vanq/logs/vanq-cli.log`:
```
2025-10-02 14:15:32 [INFO] Command executed: role list
2025-10-02 14:15:32 [DEBUG] Profile: prod, Endpoint: https://api.vanq.io
2025-10-02 14:15:32 [DEBUG] Token refresh not needed (expires in 45m)
2025-10-02 14:15:32 [DEBUG] HTTP GET /auth/roles - 200 OK (123ms)
2025-10-02 14:15:45 [INFO] Command executed: feature-flag set
2025-10-02 14:15:45 [WARN] Modifying feature flag in PRODUCTION environment
2025-10-02 14:15:48 [INFO] User confirmed operation
2025-10-02 14:15:48 [DEBUG] HTTP PATCH /api/admin/feature-flags/new-feature-beta - 200 OK (89ms)
```

## 19.3 Exit Codes
| Código | Significado | Exemplo |
|--------|-------------|---------|
| 0 | Sucesso | `vanq role list` retorna dados |
| 1 | Erro genérico | Falha na rede, timeout |
| 2 | Autenticação falhou | Token inválido ou expirado sem refresh |
| 3 | Permissão negada | Usuário sem RBAC permission necessária |
| 4 | Recurso não encontrado | `vanq role get <invalid-id>` |
| 5 | Validação falhou | Entrada inválida (ex.: email mal formatado) |

# 20. Instalação e Distribuição

## 20.1 Como .NET Tool
```bash
# Instalação global
dotnet tool install -g vanq-cli

# Atualização
dotnet tool update -g vanq-cli

# Desinstalação
dotnet tool uninstall -g vanq-cli

# Verificação
vanq --version
# vanq-cli version 0.1.0
```

## 20.2 Build Local (Desenvolvimento)
```bash
# Clonar repositório
git clone https://github.com/vanq/vanq-backend
cd vanq-backend/tools/Vanq.CLI

# Restaurar e buildar
dotnet restore
dotnet build

# Executar localmente
dotnet run -- login

# Publicar standalone (Windows)
dotnet publish -c Release -r win-x64 --self-contained

# Publicar standalone (Linux)
dotnet publish -c Release -r linux-x64 --self-contained

# Publicar standalone (macOS)
dotnet publish -c Release -r osx-x64 --self-contained
```

# 21. Prompt Copilot (Resumo)
Copilot: Implementar SPEC-0012-FEAT-cli-tool criando ferramenta CLI `vanq` em .NET 10 usando System.CommandLine e Spectre.Console. Implementar autenticação segura com armazenamento criptografado de credenciais (DPAPI + AES-256), comandos CRUD para roles, permissions, feature flags e system parameters, suporte a múltiplos profiles, refresh automático de tokens, formatação de output (json/table/csv), retry logic, modo verbose e help completo. Consumir endpoints existentes da Vanq.API respeitando permissões RBAC. Garantir funcionamento cross-platform (Windows/Linux/macOS). Publicar como .NET Tool global. Respeitar NFR-01..06 e BR-01..05.

Fim.
