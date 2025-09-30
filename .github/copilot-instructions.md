# GitHub Copilot Guide – Vanq.Backend

> Use esta ficha para deixar agentes produtivos sem vasculhar o repositório inteiro; atualize sempre que arquitetura, fluxos ou dependências mudarem.

## Snapshot
- Plataforma ASP.NET Core Minimal API (`Vanq.API`) rodando em `.NET 10.0 rc` com autenticação JWT + refresh tokens.
- Arquitetura em camadas: `Vanq.Domain` (entidades `User`, `RefreshToken`), `Vanq.Application` (contratos + abstrações), `Vanq.Infrastructure` (EF Core/Npgsql, serviços de auth) e `Vanq.Shared` (helpers comuns).
- Documentação interativa via Scalar em `/scalar`; `Program.cs` expõe somente o grupo `/auth`.

## Architecture map
- `Program.cs` registra infraestrutura via `AddInfrastructure`, configura OpenAPI/Scalar e JWT Bearer com chaves de `appsettings.json`.
- `Vanq.API/Endpoints/AuthEndpoints.cs` define rotas `/auth/register|login|refresh|logout|me` consumindo `IAuthService` / `IAuthRefreshService` e converte respostas com `AuthResultExtensions`.
- `Vanq.Infrastructure.Auth` implementa os serviços: `AuthService` (cadastro/login/logout), `AuthRefreshService` (rotação), `RefreshTokenService` (emissão/validação) e `JwtTokenService`.
- `Vanq.Infrastructure.DependencyInjection.ServiceCollectionExtensions` é o ponto único para wire-up (EF Core, repositórios, BCrypt hasher, clock).

## Auth module blueprint
- Fluxo de cadastro: normaliza e-mail, valida duplicidade (`IUserRepository.ExistsByEmailAsync`), cria `User`, gera tokens (`IJwtTokenService`, `IRefreshTokenService`).
- Login e refresh exigem usuário ativo; tokens de atualização são hashados (SHA-256) e validados contra o `SecurityStamp` salvo na entidade.
- Logout chama `RefreshTokenService.RevokeAsync`; ao expandir endpoints authenticated, extraia `userId`/`email` via `ClaimsPrincipalExtensions` em `Vanq.Shared.Security`.
- Ao introduzir novos casos, retorne `AuthResult<T>` para reutilizar o pipeline de erros (`AuthError` → HTTP map).

## Persistence & migrations
- `AppDbContext` implementa `IUnitOfWork` e carrega configs EF via `ApplyConfigurationsFromAssembly`; migrations vivem em `Vanq.Infrastructure/Migrations`.
- Repositórios (`UserRepository`, `RefreshTokenRepository`) usam EF Core AsTracking/AsNoTracking conforme necessário; testes unitários (`tests/Vanq.Infrastructure.Tests`) exercitam esses comportamentos com `UseInMemoryDatabase`.
- Atualize o banco com `dotnet ef database update --project Vanq.Infrastructure --startup-project Vanq.API` (requer PostgreSQL local e SDK preview).

## Configuration & secrets
- Defina `Jwt.SigningKey` para um segredo ≥ 32 chars; o default em `appsettings.json` é placeholder. Issuer/Audience precisam casar com consumidores.
- `ConnectionStrings:DefaultConnection` aponta para `localhost`/PostgreSQL; ajuste para ambientes não locais.
- `Jwt.AccessTokenMinutes` e `Jwt.RefreshTokenDays` controlam expiração e são usados tanto pelo middleware quanto por `RefreshTokenService`.

## Developer workflows
- Build/restore: `dotnet build Vanq.Backend.slnx` (exige SDK .NET 10 preview + workloads aspiradas rc `Microsoft.AspNetCore.*`).
- Executar API: `dotnet run --project Vanq.API` e acessar `/scalar` para doc ou `/auth/*` via `Vanq.API.http`.
- Testes rápidos: `dotnet test tests/Vanq.Infrastructure.Tests/Vanq.Infrastructure.Tests.csproj` (xUnit + FluentAssertions + EF Core InMemory).
- Para gerar novas migrations, execute `dotnet ef migrations add <Name> --project Vanq.Infrastructure --startup-project Vanq.API`.

## Coding patterns
- Use records para DTOs (`AuthResponseDto`, `RegisterUserDto`) e mantenha entidades com invariantes internas (`User.Create`, `RefreshToken.Issue`).
- Centralize horários via `IDateTimeProvider`; prefira injetar em serviços em vez de `DateTime.UtcNow` direto.
- Normalize e-mails para lower-case antes de persistir/consultar; vide `AuthService.RegisterAsync`.
- Evite acessar DbContext diretamente fora da infraestrutura — dependa de `IUserRepository`/`IRefreshTokenRepository`.
- Configure endpoints em grupos com `MapGroup` + `WithSummary` e descreva respostas com `.Produces<>` para manter OpenAPI consistente.

## Reference assets
- Guia de persistência: `docs/persistence.md` (migrations, índices e próximos passos).
- Specs e templates: `specs/` e `templates/` (IDs `SPEC-XXXX`, prompts Copilot-ready).
- Transformador OpenAPI: `Vanq.API/OpenApi/BearerAuthenticationDocumentTransformer.cs` mostra como manter o esquema bearer alinhado.

_Última revisão: 30/09/2025_
