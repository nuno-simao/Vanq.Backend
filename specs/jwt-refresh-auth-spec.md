# Especificação JWT + Refresh Tokens (Camadas Vanq)

Este documento atualiza a especificação anterior para se alinhar à estrutura de solução proposta:

```
Raiz (nenhum arquivo direto aqui)
├─ Vanq.API/             # Host Minimal API + configuração OpenAPI/Scalar + mapeamento de endpoints
├─ Vanq.Application/     # Casos de uso / Orquestração (DTOs, handlers) - (a implementar incrementalmente)
├─ Vanq.Domain/          # Entidades, regras de negócio puras (User, RefreshToken, invariantes)
├─ Vanq.Infrastructure/  # Persistência (EF Core), implementações de serviços (JWT, hashing, DateTime, repos)
└─ Vanq.Shared/          # Utilidades transversais (Result, abstrações)
```

O objetivo é manter o domínio limpo, isolado de dependências externas. A API referencia Application + Infrastructure + Domain + Shared. A camada Infrastructure referencia Domain + Shared. A Application referencia Domain + Shared. Domain e Shared não referenciam outras.

---

## 1. Objetivos

Implementar autenticação baseada em JWT com suporte a refresh token com rotação segura, persistência em PostgreSQL via EF Core (Code First), exposta em Minimal API e documentada via OpenAPI/Scalar.

---

## 2. Distribuição dos Componentes por Camada

| Responsabilidade | Local |
|------------------|-------|
| Entidades (User, RefreshToken) | Vanq.Domain |
| Value Objects (se futuros) | Vanq.Domain |
| Interfaces de Serviços (IJwtTokenService, IPasswordHasher, IDateTimeProvider, IRefreshTokenService) | Vanq.Application (ou Vanq.Domain se forem core, mas aqui manteremos em Application) |
| DTOs de Entrada/Saída (RegisterUserDto, AuthResponseDto, etc.) | Vanq.Application (folder Contracts/Auth) |
| Casos de Uso (ex: AuthenticateUserHandler futuramente) | Vanq.Application |
| DbContext + Configurações EF | Vanq.Infrastructure/Persistence |
| Implementações (JwtTokenService, BcryptPasswordHasher, RefreshTokenService) | Vanq.Infrastructure/Auth |
| Migrations | Vanq.Infrastructure/Migrations |
| Extensions de DI | Vanq.Infrastructure/DependencyInjection |
| Endpoints (MapAuthEndpoints) | Vanq.API/Endpoints/Auth |
| Config OpenAPI + Scalar | Vanq.API/Program.cs |
| Result / Error abstractions | Vanq.Shared (futuro) |

Nota: Se desejar manter interfaces puramente na camada Domain (arquitetura hexagonal), mover as abstrações centrais (IJwtTokenService, IPasswordHasher, IDateTimeProvider) para Domain.Abstractions (novo subfolder). Para simplicidade inicial ficaram em Application.

---

## 3. Estrutura de Pastas (Proposta Inicial)

```
Vanq.Domain/
  Entities/
    User.cs
    RefreshToken.cs
  Constants/ (opcional)
  Exceptions/ (opcional)
Vanq.Application/
  Abstractions/
    Auth/IJwtTokenService.cs
    Auth/IPasswordHasher.cs
    Time/IDateTimeProvider.cs
    Tokens/IRefreshTokenService.cs
  Contracts/
    Auth/
      RegisterUserDto.cs
      AuthRequestDto.cs
      AuthResponseDto.cs
      RefreshTokenRequestDto.cs
  Services/ (futuros casos de uso)
Vanq.Infrastructure/
  Persistence/
    AppDbContext.cs
    Configurations/
      UserConfiguration.cs
      RefreshTokenConfiguration.cs
  Auth/
    Jwt/
      JwtOptions.cs
      JwtTokenService.cs
    Password/
      BcryptPasswordHasher.cs
    Tokens/
      RefreshTokenFactory.cs
      RefreshTokenService.cs
  DependencyInjection/
    ServiceCollectionExtensions.cs
  Migrations/
Vanq.Shared/
  Results/
    Result.cs (futuro)
Vanq.API/
  Program.cs
  Endpoints/
    AuthEndpoints.cs
  docs/
    jwt-refresh-auth-spec.md
```

---

## 4. Entidades (Vanq.Domain)

```csharp
namespace Vanq.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public string SecurityStamp { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private User() { } // EF

    private User(Guid id, string email, string passwordHash, string securityStamp, DateTime createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string passwordHash, DateTime nowUtc)
        => new(Guid.NewGuid(), email.Trim().ToLowerInvariant(), passwordHash, Guid.NewGuid().ToString("N"), nowUtc);

    public void SetPasswordHash(string newHash)
    {
        PasswordHash = newHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public void Deactivate()
    {
        IsActive = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
    }
}
```

```csharp
namespace Vanq.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string SecurityStampSnapshot { get; private set; } = null!;

    private RefreshToken() { }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTime created, DateTime expires, string securityStampSnapshot)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = created;
        ExpiresAt = expires;
        SecurityStampSnapshot = securityStampSnapshot;
    }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime nowUtc, DateTime expiresAt, string securityStampSnapshot)
        => new(Guid.NewGuid(), userId, tokenHash, nowUtc, expiresAt, securityStampSnapshot);

    public bool IsActive => RevokedAt is null && DateTime.UtcNow <= ExpiresAt;

    public void Revoke(string? replacedBy = null, DateTime? nowUtc = null)
    {
        if (RevokedAt is not null) return;
        RevokedAt = nowUtc ?? DateTime.UtcNow;
        ReplacedByTokenHash = replacedBy;
    }
}
```

---

## 5. Contratos (Vanq.Application/Contracts/Auth)

```csharp
namespace Vanq.Application.Contracts.Auth;

public sealed record RegisterUserDto(string Email, string Password);
public sealed record AuthRequestDto(string Email, string Password);
public sealed record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, string TokenType = "Bearer");
public sealed record RefreshTokenRequestDto(string RefreshToken);
```

---

## 6. Abstrações (Vanq.Application/Abstractions)

```csharp
namespace Vanq.Application.Abstractions.Auth;
public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(Guid userId, string email, string securityStamp);
}

namespace Vanq.Application.Abstractions.Auth;
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

namespace Vanq.Application.Abstractions.Time;
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

namespace Vanq.Application.Abstractions.Tokens;
public interface IRefreshTokenService
{
    Task<(string PlainRefreshToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, string securityStamp, CancellationToken ct);
    Task<(Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken ct);
    Task RevokeAsync(Guid userId, string plainRefreshToken, CancellationToken ct);
}
```

---

## 7. Infra - JwtOptions e Token Service

```csharp
namespace Vanq.Infrastructure.Auth.Jwt;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public string SigningKey { get; init; } = null!;
    public int AccessTokenMinutes { get; init; }
    public int RefreshTokenDays { get; init; }
}
```

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vanq.Application.Abstractions.Auth;

namespace Vanq.Infrastructure.Auth.Jwt;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opts;
    private readonly byte[] _keyBytes;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _opts = options.Value;
        _keyBytes = Encoding.UTF8.GetBytes(_opts.SigningKey);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(Guid userId, string email, string securityStamp)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("security_stamp", securityStamp)
        };

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(_keyBytes),
                SecurityAlgorithms.HmacSha256)
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
```

---

## 8. Infra - Password Hasher

```csharp
using Vanq.Application.Abstractions.Auth;

namespace Vanq.Infrastructure.Auth.Password;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password);
    public bool Verify(string hash, string password) => BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
}
```

---

## 9. Infra - RefreshTokenFactory + Service

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Vanq.Infrastructure.Auth.Tokens;

internal static class RefreshTokenFactory
{
    internal static (string PlainToken, string Hash, DateTime Expires) Create(int days)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var plain = Base64UrlEncoder.Encode(bytes);
        var hash = ComputeHash(plain);
        return (plain, hash, DateTime.UtcNow.AddDays(days));
    }

    internal static string ComputeHash(string token)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
    }
}
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Domain.Entities;
using Vanq.Infrastructure.Auth.Jwt;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.Auth.Tokens;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenService(AppDbContext db, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<(string PlainRefreshToken, DateTime ExpiresAtUtc)> IssueAsync(Guid userId, string securityStamp, CancellationToken ct)
    {
        var (plain, hash, exp) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var entity = RefreshToken.Issue(userId, hash, DateTime.UtcNow, exp, securityStamp);
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (plain, exp);
    }

    public async Task<(Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken ct)
    {
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid refresh token");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || user.SecurityStamp != token.SecurityStampSnapshot)
            throw new UnauthorizedAccessException("Stale refresh token");

        // Rotação
        token.Revoke(nowUtc: DateTime.UtcNow);

        // Encadeamento (opcional preencher ReplacedByTokenHash)
        var (plainNew, hashNew, expNew) = RefreshTokenFactory.Create(_jwtOptions.RefreshTokenDays);
        var newEntity = RefreshToken.Issue(user.Id, hashNew, DateTime.UtcNow, expNew, user.SecurityStamp);
        token.ReplacedByTokenHash = hashNew;

        _db.RefreshTokens.Add(newEntity);
        await _db.SaveChangesAsync(ct);

        return (user.Id, user.SecurityStamp);
    }

    public async Task RevokeAsync(Guid userId, string plainRefreshToken, CancellationToken ct)
    {
        var hash = RefreshTokenFactory.ComputeHash(plainRefreshToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);

        if (token is null) return;
        token.Revoke(nowUtc: DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}
```

---

## 10. Infra - DbContext e Configurações

```csharp
using Microsoft.EntityFrameworkCore;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.SecurityStamp).IsRequired();
    }
}
```

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.TokenHash }).IsUnique();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.ExpiresAt);
    }
}
```

---

## 11. Extensions de DI (Vanq.Infrastructure/DependencyInjection)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Infrastructure.Auth.Jwt;
using Vanq.Infrastructure.Auth.Password;
using Vanq.Infrastructure.Auth.Tokens;
using Vanq.Infrastructure.Persistence;

namespace Vanq.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<JwtOptions>(cfg.GetSection("Jwt"));

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(cfg.GetConnectionString("DefaultConnection")));

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}

internal class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

---

## 12. API - Program.cs (Vanq.API)

Pontos-chave:
- Configuração Authentication/Authorization
- OpenAPI + Scalar
- Registro de endpoints

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Vanq.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// OpenAPI
builder.Services.AddOpenApi(o =>
{
    o.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info = new()
        {
            Title = "Vanq API",
            Version = "v1",
            Description = "API com autenticação JWT + Refresh Tokens"
        };
        doc.Components ??= new();
        doc.Components.SecuritySchemes ??= new();
        doc.Components.SecuritySchemes["bearerAuth"] = new()
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Informe o token: Bearer {token}"
        };
        doc.SecurityRequirements.Add(new() { ["bearerAuth"] = Array.Empty<string>() });
        return Task.CompletedTask;
    });
});

// JWT Auth
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer(); // Necessário para gerador

var app = builder.Build();

app.MapOpenApi(); // /openapi/v1.json
app.MapScalarApiReference(); // /scalar/v1

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();
```

---

## 13. API - Endpoints (Vanq.API/Endpoints/AuthEndpoints.cs)

```csharp
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Auth;
using Vanq.Application.Abstractions.Tokens;
using Vanq.Application.Contracts.Auth;
using Vanq.Infrastructure.Persistence;

namespace Vanq.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).AllowAnonymous()
            .WithSummary("Registra novo usuário");

        group.MapPost("/login", LoginAsync).AllowAnonymous()
            .WithSummary("Autentica usuário");

        group.MapPost("/refresh", RefreshAsync).AllowAnonymous()
            .WithSummary("Rotaciona token de refresh");

        group.MapPost("/logout", LogoutAsync)
            .WithSummary("Revoga refresh token atual")
            .RequireAuthorization();

        group.MapGet("/me", MeAsync)
            .WithSummary("Dados do usuário autenticado")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterUserDto dto,
        AppDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IRefreshTokenService refreshSvc,
        CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == dto.Email.ToLower(), ct))
            return Results.BadRequest(new { error = "Email já registrado" });

        var user = Domain.Entities.User.Create(dto.Email, hasher.Hash(dto.Password), DateTime.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var (access, exp) = jwt.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);
        var (plainRefresh, _) = await refreshSvc.IssueAsync(user.Id, user.SecurityStamp, ct);

        return Results.Ok(new AuthResponseDto(access, plainRefresh, exp));
    }

    private static async Task<IResult> LoginAsync(
        AuthRequestDto dto,
        AppDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IRefreshTokenService refreshSvc,
        CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null) return Results.Unauthorized();
        if (!hasher.Verify(user.PasswordHash, dto.Password)) return Results.Unauthorized();
        if (!user.IsActive) return Results.Forbid();

        var (access, exp) = jwt.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);
        var (plainRefresh, _) = await refreshSvc.IssueAsync(user.Id, user.SecurityStamp, ct);

        return Results.Ok(new AuthResponseDto(access, plainRefresh, exp));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequestDto dto,
        IRefreshTokenService refreshSvc,
        IJwtTokenService jwt,
        AppDbContext db,
        CancellationToken ct)
    {
        (Guid userId, string securityStamp) userData;
        try
        {
            userData = await refreshSvc.ValidateAndRotateAsync(dto.RefreshToken, ct);
        }
        catch
        {
            return Results.Unauthorized();
        }

        var user = await db.Users.FirstAsync(u => u.Id == userData.userId, ct);
        var (access, exp) = jwt.GenerateAccessToken(user.Id, user.Email, user.SecurityStamp);

        // O método ValidateAndRotateAsync já emitiu novo refresh token — aqui poderíamos retorná-lo se também exposto lá.
        // Para simplificar, mudar ValidateAndRotateAsync se quiser obter novo plain token. (Refinável)
        return Results.Ok(new { accessToken = access, expiresAtUtc = exp });
    }

    private static async Task<IResult> LogoutAsync(
        RefreshTokenRequestDto dto,
        ClaimsPrincipal principal,
        IRefreshTokenService refreshSvc,
        CancellationToken ct)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (sub is null) return Results.Unauthorized();
        await refreshSvc.RevokeAsync(Guid.Parse(sub), dto.RefreshToken, ct);
        return Results.Ok();
    }

    private static IResult MeAsync(ClaimsPrincipal principal)
    {
        return Results.Ok(new
        {
            Id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
            Email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)
        });
    }
}
```

Observação: Ajustar `ValidateAndRotateAsync` para também retornar o novo refresh token plaintext se a API quiser entregar o par completo no refresh. Exemplo de alternativa: retornar `(string NewPlainRefreshToken, Guid UserId, string SecurityStamp)`.

---

## 14. Configuração appsettings.json (Vanq.API)

```json
{
  "Jwt": {
    "Issuer": "Vanq.API",
    "Audience": "Vanq.Client",
    "SigningKey": "ALTERAR_PARA_CHAVE_FORTE_MIN_32_CHARS",
    "AccessTokenMinutes": 10,
    "RefreshTokenDays": 14
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=vanq;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Secret real via user-secrets ou variáveis de ambiente.

---

## 15. Migrações

Comandos (executar de dentro do diretório da solução apontando o projeto de startup se necessário):

```
dotnet add Vanq.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add Vanq.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Vanq.API package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add Vanq.API package Scalar.AspNetCore
dotnet add Vanq.Infrastructure package BCrypt.Net-Next

dotnet ef migrations add InitialAuth -p Vanq.Infrastructure -s Vanq.API -o Migrations
dotnet ef database update -p Vanq.Infrastructure -s Vanq.API
```

---

## 16. Fluxos (Resumo Alinhado às Camadas)

| Fluxo | Camadas Principais |
|-------|--------------------|
| Registro | API → Application (hash) → Infrastructure (persist) |
| Login | API → Infrastructure (query) → Application (hash verify) → Infrastructure (emit refresh) |
| Refresh | API → Infrastructure (valida, rotaciona) → Application (gera JWT) |
| Logout | API → Infrastructure (revoga token) |
| Revogação Global (futuro) | Alterar SecurityStamp no User → tokens antigos inválidos |

---

## 17. Segurança

Mesmo conjunto de recomendações anteriores:
- Rotação obrigatória
- Hash de refresh token
- Access curto / Refresh longo
- SecurityStamp para invalidar cadeia
- ClockSkew baixo
- Chaves fora de controle de versão

---

## 18. Checklist Atualizado

- [ ] Criar projetos (se ainda não criados)
- [ ] Adicionar pacotes conforme seção 15
- [ ] Implementar entidades Domain
- [ ] Implementar DbContext + Configurations
- [ ] Criar migração
- [ ] Implementar JwtTokenService
- [ ] Implementar RefreshTokenService (retornar novo refresh token plaintext no refresh – ajustar)
- [ ] Implementar BcryptPasswordHasher
- [ ] Implementar endpoints auth
- [ ] Configurar OpenAPI + Scalar
- [ ] Testes integração (login, refresh, logout)
- [ ] Validar `/openapi/v1.json` e `/scalar/v1`
- [ ] Endurecer (Rate limiting, reuso malicioso → revogação cadeia)
- [ ] Mover lógica de endpoints para casos de uso (futuro)
- [ ] Auditoria e logs estruturados (futuro)
- [ ] Hosted service limpeza tokens expirados (futuro)

---

## 19. Próximos Incrementos Sugeridos

| Incremento | Descrição |
|------------|-----------|
| Handler Application (CQRS) | Substituir lógica inline dos endpoints por comandos (ex: RegisterUserCommandHandler) |
| Result<T> Unificado | Em Vanq.Shared para padronizar respostas e erros |
| Domain Events | Publicar evento UserRegistered → acionar envio de email |
| Token Binding | Associar refresh a fingerprint de dispositivo |
| MFA | Inclusão TOTP + enforce em login |
| Roles/Permissions | Tabela UserRoles + ClaimsMappingService |
| Reuso Token Detection | Se refresh revogado reutilizado → revogar todos após CreatedAt |

---

## 20. Diferenças vs Especificação Original

| Item | Antes | Agora |
|------|-------|-------|
| Local das Entidades | Dentro da API | Vanq.Domain |
| Serviços | Mistos na API | Implementações na Infrastructure, interfaces em Application |
| DTOs | API | Application (Contracts) |
| Rotação | Similar | Abstraída em IRefreshTokenService |
| Extensões DI | Inline Program | ServiceCollectionExtensions em Infrastructure |
| Modularidade | Monolito | Arquitetura em camadas para evolução |

---

## 21. Ajuste no Refresh para Retornar Novo Refresh Token

Se o endpoint /auth/refresh precisar devolver também o novo `refreshToken`, ajustar `ValidateAndRotateAsync`:

```csharp
public interface IRefreshTokenService
{
    Task<(string NewPlainRefreshToken, Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken ct);
}
```

E no Service:

```csharp
public async Task<(string NewPlainRefreshToken, Guid UserId, string SecurityStamp)> ValidateAndRotateAsync(string plainRefreshToken, CancellationToken ct)
{
    // ... mesmo corpo
    return (plainNew, user.Id, user.SecurityStamp);
}
```

Endpoint:

```csharp
var (newRefresh, userId, securityStamp) = await refreshSvc.ValidateAndRotateAsync(dto.RefreshToken, ct);
var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
var (access, exp) = jwt.GenerateAccessToken(user.Id, user.Email, securityStamp);
return Results.Ok(new AuthResponseDto(access, newRefresh, exp));
```

---

## 22. Testes (Escopo Inicial)

| Teste | Objetivo |
|-------|----------|
| POST /auth/register | 200 + tokens |
| POST /auth/login (válido) | 200 + tokens |
| POST /auth/login (senha errada) | 401 |
| POST /auth/refresh (válido) | 200 + novos tokens |
| POST /auth/refresh (reuso) | 401 (após implementação de detecção) |
| POST /auth/logout + reuse refresh | 401 |
| GET /auth/me com bearer válido | 200 |
| GET /auth/me sem bearer | 401 |

---

## 23. Pacotes (Resumo por Projeto)

| Projeto | Pacotes |
|---------|---------|
| Vanq.Domain | (nenhum inicialmente) |
| Vanq.Application | (nenhum inicialmente) |
| Vanq.Infrastructure | Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design, BCrypt.Net-Next |
| Vanq.API | Microsoft.AspNetCore.Authentication.JwtBearer, Scalar.AspNetCore, Swashbuckle/OpenAPI built-in minimal (AddOpenApi), (referências aos outros projetos) |
| Vanq.Shared | (futuro: Serilog abstractions, Result) |

---

## 24. Observações de Deploy

- Usar migrations rodando em step CI/CD controlado
- Chave JWT via `DOTNET_JWT_SIGNING_KEY`
- Health check (futuro) adicionando endpoint `/health`
- Configurar HTTPS obrigatório

---

Fim.