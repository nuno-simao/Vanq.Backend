# Persistência – Repositórios

## Visão geral
- `AppDbContext` agora implementa `IUnitOfWork`, permitindo commit transacional via contrato da camada de aplicação.
- Repositórios específicos foram introduzidos:
  - `IUserRepository` / `UserRepository` para operações de usuários (busca por e-mail, verificação de existência, inserção e atualização).
  - `IRefreshTokenRepository` / `RefreshTokenRepository` para manipular tokens de atualização (consulta por hash, usuário e rastreamento para revogação).
- Serviços de autenticação (`AuthService`, `AuthRefreshService` e `RefreshTokenService`) passaram a depender dos repositórios, mantendo a lógica de domínio isolada do EF Core.
- Índices adicionais criados: `RefreshTokens(UserId, CreatedAt)` para acelerar consultas por usuário ordenadas por emissão.

## Testes
- Projeto `tests/Vanq.Infrastructure.Tests` criado com xUnit + FluentAssertions.
- Cobertura inicial garante:
  - Persistência e consultas básicas no `UserRepository`.
  - Fluxo de revogação usando `RefreshTokenRepository` com entidades rastreadas.

## Próximos passos recomendados
1. Automatizar aplicação de migrações EF Core (`dotnet ef database update` em pipelines e ambientes).
2. Monitorar necessidade de novos índices (ex.: `RefreshTokens(UserId, ExpiresAt)`, `Users(CreatedAt)`), conforme crescimento real.
3. Implementar políticas de auditoria (`UpdatedAt`, `CreatedBy`) via interceptors ou base entity.
4. Avaliar fatores de custo do hash BCrypt e torná-los configuráveis via `appsettings`.
5. Expandir a suíte de testes com cenários de concorrência e validação de constraints únicas.

## Como atualizar o banco local
```powershell
dotnet ef database update --project Vanq.Infrastructure --startup-project Vanq.API
```
> Observação: a execução requer o PostgreSQL disponível em `localhost:5432`. Se o serviço não estiver rodando, a atualização falhará com `SocketException (10061)`.
