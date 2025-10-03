# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Vanq.Backend** is an ASP.NET Core Minimal API backend (.NET 10.0 RC) implementing JWT authentication with refresh tokens, RBAC (Role-Based Access Control), and feature flags. The project uses PostgreSQL for persistence via EF Core and follows Clean Architecture principles.

## Getting Started (Onboarding)

### Prerequisites

Before starting, ensure you have:

- **.NET 10 Preview SDK** installed ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **PostgreSQL 14+** running on `localhost:5432` (or update connection string)
- **IDE/Editor**: Visual Studio 2022 Preview, VS Code, or Rider
- **Git** for version control
- **Optional**: REST Client extension for VS Code to use `.http` files

### Initial Setup (First Time)

1. **Clone and Restore**
   ```bash
   git clone <repository-url>
   cd Vanq.Backend
   dotnet restore Vanq.Backend.slnx
   ```

2. **Configure Database**
   - Ensure PostgreSQL is running
   - Update `Vanq.API/appsettings.Development.json` if needed:
     ```json
     {
       "ConnectionStrings": {
         "DefaultConnection": "Host=localhost;Database=vanq;Username=postgres;Password=YOUR_PASSWORD"
       }
     }
     ```

3. **Apply Migrations**
   ```bash
   dotnet ef database update --project Vanq.Infrastructure --startup-project Vanq.API
   ```
   This creates the database schema and seeds initial RBAC roles/permissions.

4. **Update JWT Secret**
   In `appsettings.Development.json`, change the placeholder:
   ```json
   {
     "Jwt": {
       "SigningKey": "CHANGE_ME_TO_A_STRONG_32_CHAR_SECRET_VALUE"
     }
   }
   ```
   Use at least 32 characters for development.

5. **Build and Run**
   ```bash
   dotnet build Vanq.Backend.slnx
   dotnet run --project Vanq.API
   ```

6. **Verify Setup**
   - Navigate to `https://localhost:<port>/scalar`
   - You should see the Scalar API documentation
   - Try registering a user via `POST /auth/register`

### Quick Verification Checklist

- [ ] .NET 10 SDK installed (`dotnet --version` shows 10.0.x)
- [ ] PostgreSQL accessible (test with `psql` or GUI tool)
- [ ] Database created and migrations applied
- [ ] API starts without errors
- [ ] Scalar documentation loads at `/scalar`
- [ ] Can register a test user successfully
- [ ] Tests run successfully (`dotnet test`)

## Architecture

### Layer Structure

```
Vanq.Domain          → Core entities (User, RefreshToken, Role, Permission, FeatureFlag)
Vanq.Application     → Contracts and abstractions (interfaces for services, repositories)
Vanq.Infrastructure  → EF Core implementations, auth services, RBAC, feature flags
Vanq.Shared          → Cross-cutting utilities (security helpers, validation, normalization)
Vanq.API             → Minimal API endpoints, OpenAPI config, Program.cs
```

### Key Design Patterns

- **Repository Pattern**: `IUserRepository`, `IRefreshTokenRepository`, etc. in Application layer, implemented in Infrastructure
- **Unit of Work**: `AppDbContext` implements `IUnitOfWork` for transactional consistency
- **Result Pattern**: `AuthResult<T>` used for auth operations, maps errors to HTTP responses
- **Factory Methods**: Entities use static `Create` methods (e.g., `User.Create`, `RefreshToken.Issue`)
- **Security Stamp Pattern**: Both users and roles have security stamps; token validation checks for mismatches to invalidate compromised tokens

### Dependency Injection

All infrastructure services are registered via `AddInfrastructure(IConfiguration)` in `Vanq.Infrastructure.DependencyInjection.ServiceCollectionExtensions`. This is the single registration point for:
- EF Core DbContext with Npgsql
- Repositories (User, RefreshToken, Role, Permission, etc.)
- Auth services (AuthService, AuthRefreshService, JwtTokenService, RefreshTokenService)
- RBAC services (RoleService, PermissionService, UserRoleService)
- Feature flags (FeatureFlagService with in-memory caching)
- BCrypt password hasher
- `IDateTimeProvider` for testable time operations

### Authentication Flow

1. **Registration** (`POST /auth/register`): Validates email uniqueness, creates user with hashed password, generates JWT + refresh token
2. **Login** (`POST /auth/login`): Validates credentials, checks `User.IsActive`, returns tokens
3. **Refresh** (`POST /auth/refresh`): Validates refresh token hash against SHA-256 stored in DB, rotates tokens
4. **Logout** (`POST /auth/logout`): Revokes refresh token
5. **Token Validation**: `Program.cs` JWT middleware validates security stamp and RBAC permissions on every request

### RBAC System

- **Entities**: `Role`, `Permission`, `RolePermission` (many-to-many), `UserRole` (user-role assignment)
- **Permission Format**: `domain:resource:action` (e.g., `rbac:role:read`, `rbac:user:role:assign`)
- **System Roles**: Flagged with `IsSystemRole = true`, cannot be deleted
- **Token Integration**: JWT tokens carry `roles_stamp` claim; mismatches during validation force re-authentication
- **Endpoints**: All under `/auth` prefix (e.g., `/auth/roles`, `/auth/permissions`, `/auth/users/{userId}/roles`)
- **Seeding**: Default roles and permissions seeded from `appsettings.json` via `DatabaseInitializerHostedService`

### Feature Flags

- **Storage**: PostgreSQL via `FeatureFlag` entity with multi-environment support
- **Caching**: In-memory cache (default TTL: 60s), invalidated on writes
- **Usage**: Inject `IFeatureFlagService`, call `IsEnabledAsync(flagName)` or `GetFlagOrDefaultAsync(flagName, default)`
- **Management**: Admin endpoints at `/api/admin/feature-flags` for CRUD operations
- **RBAC Integration**: Feature flag `rbac-enabled` controls whether RBAC permission checks are enforced

## Common Development Commands

### Build and Run

```bash
# Build solution
dotnet build Vanq.Backend.slnx

# Run API (requires PostgreSQL running on localhost:5432)
dotnet run --project Vanq.API

# Access interactive API docs
# Navigate to https://localhost:<port>/scalar
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add <MigrationName> --project Vanq.Infrastructure --startup-project Vanq.API

# Apply migrations to database
dotnet ef database update --project Vanq.Infrastructure --startup-project Vanq.API

# Rollback to specific migration
dotnet ef database update <MigrationName> --project Vanq.Infrastructure --startup-project Vanq.API
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Vanq.Infrastructure.Tests/Vanq.Infrastructure.Tests.csproj

# Run with detailed output
dotnet test --verbosity detailed
```

**Test Framework**: xUnit + Shouldly (migrated from FluentAssertions) + EF Core InMemory for repository tests

## Testing Patterns and Conventions

### Test Structure

All tests follow the **AAA pattern** (Arrange-Act-Assert):

```csharp
[Fact]
public async Task MethodName_ShouldExpectedBehavior_WhenCondition()
{
    // Arrange - Setup test data and dependencies
    await using var context = CreateContext();
    var repository = new MyRepository(context);
    var testEntity = MyEntity.Create(...);

    // Act - Execute the operation being tested
    var result = await repository.SomeMethodAsync(testEntity);

    // Assert - Verify the outcome using Shouldly
    result.ShouldNotBeNull();
    result.Property.ShouldBe(expectedValue);
}
```

### Naming Convention

Test method names follow: `MethodName_ShouldExpectedBehavior_WhenCondition`

**Examples:**
- `IsEnabledAsync_ShouldReturnTrue_WhenFlagIsEnabled`
- `Create_ShouldThrowException_WhenNameIsInvalid`
- `GetByIdAsync_ShouldReturnNull_WhenEntityDoesNotExist`

### Shouldly Assertions

The project uses Shouldly for fluent, readable assertions:

```csharp
// Basic assertions
result.ShouldBe(expected);
result.ShouldNotBe(unexpected);
result.ShouldBeNull();
result.ShouldNotBeNull();

// Boolean assertions
flag.ShouldBeTrue();
flag.ShouldBeFalse();

// Collection assertions
collection.ShouldNotBeEmpty();
collection.ShouldContain(item);
collection.Count.ShouldBe(3);

// Exception assertions
Should.Throw<ArgumentException>(() => SomeMethod());
var ex = await Should.ThrowAsync<InvalidOperationException>(async () => await SomeAsyncMethod());
ex.Message.ShouldContain("expected text");

// String assertions
text.ShouldStartWith("prefix");
text.ShouldContain("substring");
email.ShouldBe("user@example.com", StringCompareShould.IgnoreCase);
```

### Repository Tests with InMemory Database

Use EF Core InMemory database for repository tests:

```csharp
public class MyRepositoryTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistEntity_WhenValidData()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var user = User.Create("test@example.com", "hashedPassword", DateTime.UtcNow);

        // Act
        await repository.AddAsync(user, CancellationToken.None);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await repository.GetByIdAsync(user.Id, CancellationToken.None);
        retrieved.ShouldNotBeNull();
        retrieved.Email.ShouldBe("test@example.com");
    }
}
```

### Service Tests with Mocked Dependencies

For services that depend on external services, use dependency injection with test doubles:

```csharp
public class FeatureFlagServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly DateTime _fixedTime = new(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public FeatureFlagServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    private FeatureFlagService CreateService(AppDbContext context, IFeatureFlagRepository repository)
    {
        var hostEnv = new FakeHostEnvironment { EnvironmentName = "Development" };
        var logger = NullLogger<FeatureFlagService>.Instance;
        return new FeatureFlagService(repository, _cache, hostEnv, logger);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnTrue_WhenFlagIsEnabled()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new FeatureFlagRepository(context);
        var service = CreateService(context, repository);

        var flag = FeatureFlag.Create("enabled-feature", "Development", true, "test", _fixedTime);
        await repository.AddAsync(flag, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        var isEnabled = await service.IsEnabledAsync("enabled-feature");

        // Assert
        isEnabled.ShouldBeTrue();
    }
}
```

### Test Organization

Tests are organized by layer and feature:

```
tests/
└── Vanq.Infrastructure.Tests/
    ├── Authorization/         # Authorization filter tests
    ├── Domain/                # Entity and domain logic tests
    ├── FeatureFlags/          # Feature flag service tests
    ├── Persistence/           # Repository tests
    └── Rbac/                  # RBAC service tests
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~FeatureFlagServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~IsEnabledAsync_ShouldReturnTrue_WhenFlagIsEnabled"
```

## Configuration

### Required Settings (`appsettings.json`)

- **`Jwt:SigningKey`**: Minimum 32 characters, change default placeholder before production
- **`Jwt:Issuer`**: Must match token consumer configuration
- **`Jwt:Audience`**: Must match token consumer configuration
- **`Jwt:AccessTokenMinutes`**: JWT token lifetime (default: 10)
- **`Jwt:RefreshTokenDays`**: Refresh token lifetime (default: 14)
- **`ConnectionStrings:DefaultConnection`**: PostgreSQL connection string
- **`Rbac:FeatureEnabled`**: Enable/disable RBAC enforcement
- **`Rbac:DefaultRole`**: Role assigned to new users during registration
- **`Rbac:Seed`**: Initial roles and permissions configuration
- **`Cors:PolicyName`**: CORS policy name (default: `vanq-default-cors`)
- **`Cors:AllowedOrigins`**: List of allowed origins (empty in Development = allow any)
- **`Cors:AllowedMethods`**: HTTP methods allowed for CORS (default: GET, POST, PUT, PATCH, DELETE, OPTIONS)
- **`Cors:AllowedHeaders`**: Request headers allowed for CORS (default: Content-Type, Authorization, Accept, etc.)
- **`Cors:AllowCredentials`**: Enable credentials (cookies, Authorization header) (default: true)
- **`Cors:MaxAgeSeconds`**: Preflight cache duration (default: 3600)

### CORS Configuration

The API supports CORS (Cross-Origin Resource Sharing) for web clients. See [docs/cors-configuration.md](docs/cors-configuration.md) for detailed configuration.

**Key points:**
- **Development**: Automatically allows any origin (no configuration needed)
- **Production**: Only HTTPS origins configured in `AllowedOrigins` are allowed
- **Feature Flag**: `cors-relaxed` can enable permissive mode (use sparingly!)
- **Logging**: Blocked CORS requests are logged with structured data

**Example Production Configuration:**
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://app.example.com",
      "https://dashboard.example.com"
    ],
    "AllowCredentials": true
  }
}
```

### Environment Detection

The application uses `IHostEnvironment` to resolve the current environment. Feature flags can have different values per environment (Development, Staging, Production).

## Coding Conventions

### Entity Invariants

- Use static factory methods: `User.Create(...)`, `RefreshToken.Issue(...)`, `Role.Create(...)`
- Entities enforce invariants internally (e.g., email normalization, required fields)
- Never expose public setters for critical properties

### Email Handling

Always normalize emails to lowercase before persistence or queries:
```csharp
var normalizedEmail = email.Trim().ToLowerInvariant();
```

### Time Operations

Inject `IDateTimeProvider` instead of using `DateTime.UtcNow` directly:
```csharp
public class MyService
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public MyService(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public void DoWork()
    {
        var now = _dateTimeProvider.UtcNow;
    }
}
```

### Endpoint Patterns

- Group endpoints with `MapGroup()` for route prefixes
- Use `.WithSummary()` and `.Produces<T>()` for OpenAPI documentation
- Extract user context from JWT claims using `ClaimsPrincipalExtensions.TryGetUserId()`
- Return typed results: `Results.Ok(dto)`, `Results.BadRequest(error)`, etc.

### Security Best Practices

- Always check `User.IsActive` before granting access
- Validate security stamps during token operations
- Hash refresh tokens with SHA-256 before storage
- Use BCrypt for password hashing (configured via DI)
- Normalize identifiers (emails, permission names) before comparisons

## Implementation Examples (Step-by-Step)

### Example 1: Adding a New Entity with Migration

Let's add a `Product` entity to the system.

**Step 1: Create the Entity** (`Vanq.Domain/Entities/Product.cs`)

```csharp
namespace Vanq.Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Product() { } // EF Core constructor

    private Product(Guid id, string name, string description, decimal price, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        CreatedAt = createdAt;
    }

    public static Product Create(string name, string description, decimal price, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (price < 0)
            throw new ArgumentException("Price must be non-negative", nameof(price));

        return new Product(Guid.NewGuid(), name, description, price, nowUtc);
    }

    public void Update(string name, string description, decimal price, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (price < 0)
            throw new ArgumentException("Price must be non-negative", nameof(price));

        Name = name;
        Description = description;
        Price = price;
        UpdatedAt = nowUtc;
    }

    public void Deactivate() => IsActive = false;
}
```

**Step 2: Create EF Core Configuration** (`Vanq.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.IsActive);
    }
}
```

**Step 3: Add DbSet to AppDbContext** (`Vanq.Infrastructure/Persistence/AppDbContext.cs`)

```csharp
public DbSet<Product> Products => Set<Product>();
```

**Step 4: Create and Apply Migration**

```bash
dotnet ef migrations add AddProductEntity --project Vanq.Infrastructure --startup-project Vanq.API
dotnet ef database update --project Vanq.Infrastructure --startup-project Vanq.API
```

**Step 5: Create Repository Interface** (`Vanq.Application/Abstractions/Persistence/IProductRepository.cs`)

```csharp
namespace Vanq.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Product>> GetAllActiveAsync(CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
    void Update(Product product);
    void Delete(Product product);
}
```

**Step 6: Implement Repository** (`Vanq.Infrastructure/Persistence/Repositories/ProductRepository.cs`)

```csharp
using Microsoft.EntityFrameworkCore;
using Vanq.Application.Abstractions.Persistence;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Product>> GetAllActiveAsync(CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    public void Update(Product product)
    {
        _context.Products.Update(product);
    }

    public void Delete(Product product)
    {
        _context.Products.Remove(product);
    }
}
```

**Step 7: Register Repository in DI** (`Vanq.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`)

```csharp
services.AddScoped<IProductRepository, ProductRepository>();
```

### Example 2: Creating a Complete Endpoint with RBAC Protection

**Step 1: Create DTOs** (`Vanq.Application/Contracts/Products/`)

```csharp
// ProductDto.cs
public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    bool IsActive,
    DateTime CreatedAt
);

// CreateProductRequest.cs
public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price
);
```

**Step 2: Create Service Interface** (`Vanq.Application/Abstractions/Products/IProductService.cs`)

```csharp
namespace Vanq.Application.Abstractions.Products;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<ProductDto>> GetAllActiveAsync(CancellationToken cancellationToken);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
}
```

**Step 3: Implement Service** (`Vanq.Infrastructure/Products/ProductService.cs`)

```csharp
using Vanq.Application.Abstractions.Persistence;
using Vanq.Application.Abstractions.Products;
using Vanq.Application.Abstractions.Time;
using Vanq.Application.Contracts.Products;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Products;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public ProductService(
        IProductRepository repository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        return product == null ? null : MapToDto(product);
    }

    public async Task<List<ProductDto>> GetAllActiveAsync(CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllActiveAsync(cancellationToken);
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = Product.Create(request.Name, request.Description, request.Price, _clock.UtcNow);

        await _repository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(product);
    }

    private static ProductDto MapToDto(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.IsActive,
        product.CreatedAt
    );
}
```

**Step 4: Create Endpoints with RBAC** (`Vanq.API/Endpoints/ProductEndpoints.cs`)

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Vanq.API.Authorization;
using Vanq.Application.Abstractions.Products;
using Vanq.Application.Contracts.Products;

namespace Vanq.API.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder apiRoute)
    {
        var group = apiRoute.MapGroup("/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapGet("/", GetAllProductsAsync)
            .WithSummary("Lists all active products")
            .Produces<List<ProductDto>>(StatusCodes.Status200OK)
            .RequirePermission("product:read");

        group.MapGet("/{id:guid}", GetProductByIdAsync)
            .WithSummary("Gets a product by ID")
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequirePermission("product:read");

        group.MapPost("/", CreateProductAsync)
            .WithSummary("Creates a new product")
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .RequirePermission("product:create");

        return group;
    }

    private static async Task<IResult> GetAllProductsAsync(
        IProductService productService,
        CancellationToken cancellationToken)
    {
        var products = await productService.GetAllActiveAsync(cancellationToken);
        return Results.Ok(products);
    }

    private static async Task<IResult> GetProductByIdAsync(
        Guid id,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        var product = await productService.GetByIdAsync(id, cancellationToken);
        return product == null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> CreateProductAsync(
        CreateProductRequest request,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(request, cancellationToken);
        return Results.Created($"/products/{product.Id}", product);
    }
}
```

**Step 5: Register Endpoints** (in `Vanq.API/Endpoints/Endpoints.cs`)

```csharp
public static void MapAllEndpoints(this IEndpointRouteBuilder app)
{
    var apiRoute = app.MapGroup("/api");

    apiRoute.MapAuthEndpoints();
    apiRoute.MapProductEndpoints(); // Add this line
    // ... other endpoints
}
```

**Step 6: Register Service in DI**

```csharp
services.AddScoped<IProductService, ProductService>();
```

**Step 7: Seed Required Permissions** (in `appsettings.json`)

```json
{
  "Rbac": {
    "Seed": {
      "Permissions": [
        {
          "Name": "product:read",
          "DisplayName": "Read products",
          "Description": "View product catalog"
        },
        {
          "Name": "product:create",
          "DisplayName": "Create products",
          "Description": "Add new products"
        }
      ]
    }
  }
}
```

## Project Structure Notes

### Endpoints Organization

All endpoints are registered in `Vanq.API/Endpoints/`:
- `AuthEndpoints.cs` → `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me`
- `FeatureFlagsEndpoints.cs` → `/api/admin/feature-flags/*`
- `RolesEndpoints.cs` → `/auth/roles/*`
- `PermissionsEndpoints.cs` → `/auth/permissions/*`
- `UserRoleEndpoints.cs` → `/auth/users/{userId}/roles/*`

Extension method `MapAllEndpoints()` in `Endpoints.cs` registers all endpoint groups.

### Key Infrastructure Services

- **`AuthService`**: User registration, login, logout
- **`AuthRefreshService`**: Token refresh and rotation
- **`JwtTokenService`**: JWT token generation with claims
- **`RefreshTokenService`**: Refresh token issuance, validation, revocation
- **`FeatureFlagService`**: Feature flag resolution with caching
- **`RoleService`**: Role CRUD with permission management
- **`PermissionService`**: Permission CRUD
- **`UserRoleService`**: User-role assignment/revocation

### Shared Utilities (`Vanq.Shared`)

The `Vanq.Shared` project contains cross-cutting utilities used across all layers.

#### ClaimsPrincipalExtensions

Extract user context from JWT tokens in endpoints:

```csharp
// In an endpoint handler
private static async Task<IResult> MyEndpoint(ClaimsPrincipal principal, ...)
{
    // Extract just userId
    if (!principal.TryGetUserId(out var userId))
    {
        return Results.Unauthorized();
    }

    // Extract both userId and email
    if (!principal.TryGetUserContext(out var userId, out var email))
    {
        return Results.Unauthorized();
    }

    // Use userId for business logic
    await someService.ProcessAsync(userId);
}
```

#### StringNormalizationUtils

Normalize user input before persistence or queries:

```csharp
// Normalize emails (lowercase, trimmed)
var normalizedEmail = StringNormalizationUtils.NormalizeEmail("User@Example.COM");
// Result: "user@example.com"

// Normalize names (titlecase, trimmed)
var normalizedName = StringNormalizationUtils.NormalizeName("  joão  silva  ");
// Result: "João Silva"
```

#### NamingValidationUtils

Validate RBAC naming conventions:

```csharp
// Validate role names: ^[a-z][a-z0-9-_]+$
NamingValidationUtils.ValidateRoleName("admin-user"); // OK
NamingValidationUtils.ValidateRoleName("Admin");      // Throws ArgumentException

// Validate permission names: domain:resource:action[:context]
NamingValidationUtils.ValidatePermissionName("rbac:role:read");    // OK
NamingValidationUtils.ValidatePermissionName("InvalidFormat");     // Throws

// Non-throwing versions
bool isValid = NamingValidationUtils.IsValidRoleName("viewer");
bool isValid = NamingValidationUtils.IsValidPermissionName("rbac:user:role:assign");
```

#### CacheKeyUtils

Build consistent cache keys for distributed scenarios:

```csharp
// Feature flag keys
var key = CacheKeyUtils.BuildFeatureFlagKey("Development", "my-feature");
// Result: "feature-flag:Development:my-feature"

// User-related keys
var userKey = CacheKeyUtils.BuildUserKey(userId);
var userPermKey = CacheKeyUtils.BuildUserKey(userId, "permissions");
// Results: "user:<guid>" and "user:<guid>:permissions"

// Custom keys
var customKey = CacheKeyUtils.BuildKey("myprefix", "segment1", "segment2");
// Result: "myprefix:segment1:segment2"
```

#### GuidValidationUtils

Safe GUID parsing with validation:

```csharp
// Parse and validate in one step
if (GuidValidationUtils.TryParseAndValidate(input, out var guid))
{
    // guid is valid and not Guid.Empty
}

// Throws if invalid
var guid = GuidValidationUtils.ParseAndValidate(input);
```

#### SecurityStampUtils

Generate cryptographically secure stamps (used internally by entities):

```csharp
var stamp = SecurityStampUtils.Generate();
// Returns a Base64-encoded random string (32 bytes)
```

## Documentation

### Primary Documentation (`docs/`)

- **`persistence.md`**: EF Core migrations, repositories, indexes, best practices
- **`feature-flags.md`**: Feature flag architecture, usage, API endpoints
- **`rbac-overview.md`**: RBAC concepts, permission format, security stamps
- **`implementation-order-summary.md`**: Recommended order for implementing specs 1-10
- **`SPEC-*-validation-report.md`**: Validation reports for implemented specs

### Specifications (`specs/`)

All feature specs follow format `SPEC-XXXX-FEAT-<name>.md`. Implemented specs include:
- **SPEC-0006**: Feature Flags (v0.2.0 - includes audit endpoints)
- **SPEC-0011**: RBAC

Pending specs (1-5, 7-10) are documented in `docs/implementation-order-summary.md` with dependency analysis.

### Templates (`templates/`)

Reusable templates for documentation, specs, and validation reports.

## Important Notes

### .NET 10 RC Dependencies

This project uses .NET 10.0 RC packages. Ensure the .NET 10 Preview SDK is installed. Package versions are centrally managed in `Directory.Packages.props`.

### Database Requirement

PostgreSQL must be running on `localhost:5432` for local development. Update `ConnectionStrings:DefaultConnection` for other environments.

### OpenAPI Documentation

- **Interactive docs**: Available at `/scalar` (Scalar UI)
- **OpenAPI JSON**: Available at `/openapi/v1.json`
- **Bearer auth**: Configured via `BearerAuthenticationDocumentTransformer` in `Vanq.API/OpenApi/`

### HTTP Testing

Use `Vanq.API/Vanq.API.http` file with REST Client or similar tools for endpoint testing.

## Security Deep Dive

### Security Stamp Validation Flow

Security stamps are critical for invalidating tokens when sensitive user or role data changes.

**When Security Stamps Are Rotated:**

1. **User Security Stamp** (`User.SecurityStamp`):
   - Password change (`User.SetPasswordHash()`)
   - User deactivation (`User.Deactivate()`)
   - Role assignment (`User.AssignRole()`)
   - Role revocation (`User.RevokeRole()`)

2. **Role Security Stamp** (`Role.SecurityStamp`):
   - Role permissions updated (`Role.UpdatePermissions()`)

**Token Validation Flow** (in `Program.cs` JWT middleware):

```
1. JWT Bearer middleware extracts token
   ↓
2. Validates signature, expiration, issuer, audience
   ↓
3. OnTokenValidated event fires
   ↓
4. Extract userId from "sub" claim
   ↓
5. Load User with Roles from database
   ↓
6. Compare token's security_stamp claim with User.SecurityStamp
   ↓
7. If mismatch → Fail("Security stamp mismatch")
   ↓
8. If RBAC enabled (feature flag "rbac-enabled")
   ↓
9. Build current RBAC payload (roles + permissions + roles_stamp)
   ↓
10. Compare token's roles_stamp with computed roles_stamp
   ↓
11. If mismatch → Fail("RBAC permissions outdated")
   ↓
12. Replace token's role/permission claims with fresh data
   ↓
13. Request proceeds with up-to-date claims
```

**Key Implementation** (from `Vanq.API/Program.cs:48-117`):

```csharp
options.Events = new JwtBearerEvents
{
    OnTokenValidated = async context =>
    {
        // Extract userId
        var principal = context.Principal;
        if (!principal.TryGetUserId(out var userId))
        {
            context.Fail("Missing user context");
            return;
        }

        // Check security stamp
        var securityStampClaim = principal.FindFirst("security_stamp")?.Value;
        var user = await userRepository.GetByIdWithRolesAsync(userId, ct);

        if (!string.Equals(user.SecurityStamp, securityStampClaim, StringComparison.Ordinal))
        {
            context.Fail("Security stamp mismatch");
            return;
        }

        // Check RBAC roles_stamp if enabled
        if (await featureFlagService.IsEnabledAsync("rbac-enabled"))
        {
            var tokenRolesStamp = principal.FindFirst("roles_stamp")?.Value ?? string.Empty;
            var (roles, permissions, rolesStamp) = RbacTokenPayloadBuilder.Build(user);

            if (!string.Equals(rolesStamp, tokenRolesStamp, StringComparison.Ordinal))
            {
                context.Fail("RBAC permissions outdated");
                return;
            }

            // Refresh claims with current roles/permissions
            // ...
        }
    }
};
```

### Token Revocation Scenarios

**Scenario 1: User Changes Password**

```csharp
// In password reset service
var user = await _userRepository.GetByIdAsync(userId);
user.SetPasswordHash(newPasswordHash); // Rotates SecurityStamp internally
await _unitOfWork.SaveChangesAsync();

// Result: All existing JWT tokens for this user become invalid
// User must login again with new password to get fresh tokens
```

**Scenario 2: Admin Changes User Roles**

```csharp
// In UserRoleService
var user = await _userRepository.GetByIdWithRolesAsync(userId);
user.AssignRole(roleId, adminId, now); // Rotates SecurityStamp
await _unitOfWork.SaveChangesAsync();

// Result: User's JWT tokens become invalid due to:
// 1. SecurityStamp mismatch (user stamp changed)
// 2. roles_stamp mismatch (roles changed)
// User must refresh token or re-login
```

**Scenario 3: Admin Updates Role Permissions**

```csharp
// In RoleService
var role = await _roleRepository.GetByIdWithPermissionsAsync(roleId);
role.UpdatePermissions(newPermissionIds, adminId, now); // Rotates Role.SecurityStamp
await _unitOfWork.SaveChangesAsync();

// Result: All users with this role have mismatched roles_stamp
// They must refresh tokens to get updated permissions
```

**Scenario 4: Explicit Logout**

```csharp
// POST /auth/logout
await _refreshTokenService.RevokeAsync(refreshToken); // Marks token as revoked in DB

// Result: Refresh token cannot be used again
// Access token remains valid until expiration (max 10 min by default)
```

### Refresh Token Security

Refresh tokens use **double hashing** for security:

1. **Generation** (`RefreshTokenService.IssueAsync`):
   ```
   Generate 32 random bytes → Base64 encode → plainToken
   SHA-256(plainToken) → Store tokenHash in database
   Return plainToken to client
   ```

2. **Validation** (`RefreshTokenService.ValidateAsync`):
   ```
   Client sends plainToken
   SHA-256(plainToken) → Compute hash
   Query database for matching tokenHash
   Check expiration, revocation status
   Validate SecurityStamp against User
   ```

3. **Rotation** (on refresh):
   ```
   Validate old refresh token
   Revoke old token in database
   Issue new refresh token
   Return new access + refresh tokens
   ```

**Why This Matters:**
- Database only stores hashes, not plaintext tokens
- If database is compromised, attackers cannot use tokens directly
- Tokens are single-use (revoked after rotation)
- Lost tokens cannot be recovered (re-login required)

### RBAC Permission Enforcement

**Permission Check Flow** (via `RequirePermission` endpoint filter):

```
1. Endpoint decorated with .RequirePermission("domain:resource:action")
   ↓
2. PermissionEndpointFilter runs before handler
   ↓
3. Extract ClaimsPrincipal from HttpContext
   ↓
4. Check if RBAC feature flag is enabled
   ↓
5. Find "permission" claims in token
   ↓
6. If required permission is present → Continue
   ↓
7. If missing → Return 403 Forbidden
```

**Implementation** (`Vanq.API/Authorization/PermissionEndpointFilter.cs`):

```csharp
public async ValueTask<object?> InvokeAsync(
    EndpointFilterInvocationContext context,
    EndpointFilterDelegate next)
{
    var user = context.HttpContext.User;
    var featureFlagService = context.HttpContext.RequestServices
        .GetRequiredService<IFeatureFlagService>();

    if (!await featureFlagService.IsEnabledAsync("rbac-enabled"))
    {
        return await next(context); // RBAC disabled, allow all
    }

    var hasPermission = user.Claims
        .Any(c => c.Type == "permission" && c.Value == _requiredPermission);

    if (!hasPermission)
    {
        return Results.Forbid();
    }

    return await next(context);
}
```

### Password Hashing (BCrypt)

**Configuration:**
- Work factor: Default (configured in BCrypt.Net library)
- Salt: Auto-generated per password
- Registered in DI as `IPasswordHasher` → `BcryptPasswordHasher`

**Usage:**
```csharp
// Hashing
var hashedPassword = _passwordHasher.Hash("plaintextPassword");

// Verification
bool isValid = _passwordHasher.Verify("plaintextPassword", hashedPassword);
```

**Important:** Never log or expose hashed passwords. They should only exist in memory during authentication.

## API Endpoints Reference

### Authentication Endpoints

| Endpoint | Method | Auth | Permission | Description |
|----------|--------|------|------------|-------------|
| `/auth/register` | POST | ❌ | None | Register new user, returns JWT + refresh token |
| `/auth/login` | POST | ❌ | None | Authenticate user, returns tokens |
| `/auth/refresh` | POST | ❌ | None | Rotate refresh token, returns new tokens |
| `/auth/logout` | POST | ✅ | None | Revoke refresh token |
| `/auth/me` | GET | ✅ | None | Get current user info |

### RBAC - Roles Endpoints

| Endpoint | Method | Auth | Permission | Description |
|----------|--------|------|------------|-------------|
| `/auth/roles` | GET | ✅ | `rbac:role:read` | List all roles |
| `/auth/roles` | POST | ✅ | `rbac:role:create` | Create new role |
| `/auth/roles/{roleId}` | PATCH | ✅ | `rbac:role:update` | Update role details/permissions |
| `/auth/roles/{roleId}` | DELETE | ✅ | `rbac:role:delete` | Delete non-system role |

### RBAC - Permissions Endpoints

| Endpoint | Method | Auth | Permission | Description |
|----------|--------|------|------------|-------------|
| `/auth/permissions` | GET | ✅ | `rbac:permission:read` | List all permissions |
| `/auth/permissions` | POST | ✅ | `rbac:permission:create` | Create new permission |
| `/auth/permissions/{permissionId}` | PATCH | ✅ | `rbac:permission:update` | Update permission metadata |
| `/auth/permissions/{permissionId}` | DELETE | ✅ | `rbac:permission:delete` | Delete permission |

### RBAC - User Roles Endpoints

| Endpoint | Method | Auth | Permission | Description |
|----------|--------|------|------------|-------------|
| `/auth/users/{userId}/roles` | POST | ✅ | `rbac:user:role:assign` | Assign role to user |
| `/auth/users/{userId}/roles/{roleId}` | DELETE | ✅ | `rbac:user:role:revoke` | Revoke role from user |

### Feature Flags Endpoints (Admin)

| Endpoint | Method | Auth | Permission | Description |
|----------|--------|------|------------|-------------|
| `/api/admin/feature-flags` | GET | ✅ | `system:feature-flags:read` | List all flags (all environments) |
| `/api/admin/feature-flags/current` | GET | ✅ | `system:feature-flags:read` | List flags for current environment |
| `/api/admin/feature-flags` | POST | ✅ | `system:feature-flags:write` | Create new flag |
| `/api/admin/feature-flags/{key}` | PATCH | ✅ | `system:feature-flags:write` | Update flag value |
| `/api/admin/feature-flags/{key}` | DELETE | ✅ | `system:feature-flags:write` | Delete flag |

**Legend:**
- ✅ = Requires authentication (Bearer token)
- ❌ = Anonymous access
- Permission = Required RBAC permission (enforced if `rbac-enabled` flag is true)

## Troubleshooting

### Migration Errors

**Problem: `SocketException (10061): No connection could be made`**

```
System.Net.Sockets.SocketException (10061): No connection could be made because the target machine actively refused it.
```

**Solution:**
1. Ensure PostgreSQL service is running:
   ```bash
   # Windows
   net start postgresql-x64-14

   # macOS/Linux
   sudo systemctl start postgresql
   ```
2. Verify PostgreSQL is listening on correct port (default 5432)
3. Check firewall settings allow local connections

**Problem: `Npgsql.NpgsqlException: 28P01: password authentication failed`**

**Solution:**
1. Verify credentials in `appsettings.json` match PostgreSQL user
2. Reset PostgreSQL password if needed:
   ```sql
   ALTER USER postgres WITH PASSWORD 'newpassword';
   ```
3. Ensure connection string format is correct:
   ```
   Host=localhost;Database=vanq;Username=postgres;Password=yourpassword
   ```

**Problem: `Migration already exists with name 'MigrationName'`**

**Solution:**
1. Remove migration files from `Vanq.Infrastructure/Migrations/`
2. Recreate migration:
   ```bash
   dotnet ef migrations add NewMigrationName --project Vanq.Infrastructure --startup-project Vanq.API
   ```

**Problem: `Pending model changes detected`**

```
The model has pending changes. Add a new migration before updating the database.
```

**Solution:**
1. Create migration for pending changes:
   ```bash
   dotnet ef migrations add DescriptiveChangeName --project Vanq.Infrastructure --startup-project Vanq.API
   ```
2. Apply migration:
   ```bash
   dotnet ef database update --project Vanq.Infrastructure --startup-project Vanq.API
   ```

### Token Validation Failures

**Problem: 401 Unauthorized with "Security stamp mismatch"**

**Cause:** User's `SecurityStamp` changed after token was issued (password change, role assignment, deactivation).

**Solution:**
1. User must re-login to get fresh tokens:
   ```http
   POST /auth/login
   Content-Type: application/json

   {
     "email": "user@example.com",
     "password": "password"
   }
   ```
2. Or use refresh token if still valid:
   ```http
   POST /auth/refresh
   Content-Type: application/json

   {
     "refreshToken": "your-refresh-token"
   }
   ```

**Problem: 401 Unauthorized with "RBAC permissions outdated"**

**Cause:** User's roles or role permissions changed after token was issued.

**Solution:**
1. Call refresh endpoint to get updated tokens with current permissions:
   ```http
   POST /auth/refresh
   ```
2. If refresh token is also stale, re-login required

**Problem: 403 Forbidden on protected endpoint**

**Cause:** User lacks required RBAC permission for the endpoint.

**Solution:**
1. Check user's assigned roles:
   ```http
   GET /auth/me
   Authorization: Bearer {access-token}
   ```
2. Verify role has required permission:
   ```http
   GET /auth/roles
   Authorization: Bearer {admin-token}
   ```
3. Assign missing permission to role, or assign role with permission to user

**Problem: Token works in Postman but fails in application**

**Cause:** Bearer token format incorrect or missing.

**Solution:**
Ensure header format is exactly:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```
Not:
```
Authorization: eyJhbGci... (missing "Bearer ")
Bearer eyJhbGci... (missing "Authorization:" header name)
```

### BCrypt / Password Hashing Errors

**Problem: `BCrypt.Net.SaltParseException: Invalid salt version`**

**Cause:** Trying to verify against an invalid or corrupted hash.

**Solution:**
1. Ensure hash is stored correctly in database (60-character string)
2. Never manually edit password hashes
3. Use `_passwordHasher.Hash()` to generate fresh hash

**Problem: Login extremely slow (>2 seconds)**

**Cause:** BCrypt work factor too high for environment.

**Solution:**
BCrypt is intentionally slow for security. Current performance is normal. If absolutely needed, adjust work factor in `BcryptPasswordHasher` (not recommended for production):

```csharp
// Lower work factor = faster but less secure
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10); // Default is 11
```

**Problem: Password hash not validating correctly**

**Cause:** Encoding mismatch or extra whitespace.

**Solution:**
```csharp
// Correct usage
var isValid = _passwordHasher.Verify(plaintextPassword.Trim(), storedHash);

// Common mistake: comparing hashes directly
if (hash1 == hash2) // WRONG - BCrypt generates unique salt each time
```

### Feature Flag Issues

**Problem: Flag always returns false despite being enabled in DB**

**Cause 1:** Environment mismatch

**Solution:**
```bash
# Check current environment
dotnet run --project Vanq.API --environment Development

# Or set environment variable
# Windows
$env:ASPNETCORE_ENVIRONMENT="Development"

# macOS/Linux
export ASPNETCORE_ENVIRONMENT=Development
```

Feature flag in DB must match environment name exactly (case-sensitive).

**Cause 2:** Cache serving stale data

**Solution:**
1. Wait for cache TTL (default 60 seconds) to expire
2. Or restart application to clear cache
3. Or update flag via API to invalidate cache immediately:
   ```http
   PATCH /api/admin/feature-flags/my-flag
   Authorization: Bearer {admin-token}
   Content-Type: application/json

   {
     "isEnabled": true
   }
   ```

**Problem: Feature flag endpoint returns 403 Forbidden**

**Cause:** User lacks `system:feature-flags:read` or `system:feature-flags:write` permission.

**Solution:**
1. Feature flags are admin-only by default
2. Assign required permissions to user's role in `appsettings.json` seed configuration
3. Re-run migrations to apply seed data

**Problem: Flag exists but `IsEnabledAsync` returns false**

**Cause:** Fallback behavior on error or missing flag.

**Solution:**
1. Check logs for exceptions (database connection, EF Core errors)
2. Verify flag exists for current environment:
   ```http
   GET /api/admin/feature-flags/current
   ```
3. Use `GetFlagOrDefaultAsync` with explicit default if needed:
   ```csharp
   var isEnabled = await _featureFlags.GetFlagOrDefaultAsync("my-flag", defaultValue: false);
   ```

### EF Core / Database Issues

**Problem: `DbUpdateConcurrencyException: Database operation expected to affect 1 row(s) but actually affected 0 row(s)`**

**Cause:** Entity was modified or deleted by another process between load and save.

**Solution:**
1. Reload entity before retry:
   ```csharp
   await context.Entry(entity).ReloadAsync();
   ```
2. Implement optimistic concurrency with `[Timestamp]` or `[ConcurrencyCheck]` if frequent

**Problem: `InvalidOperationException: A second operation started on this context before a previous operation completed`**

**Cause:** Async operations not properly awaited, or DbContext used concurrently.

**Solution:**
1. Ensure all async calls are awaited:
   ```csharp
   // Correct
   await repository.AddAsync(user, ct);
   await unitOfWork.SaveChangesAsync(ct);

   // Wrong - missing await
   repository.AddAsync(user, ct); // Fire and forget
   ```
2. DbContext is not thread-safe - use scoped lifetime (already configured)

**Problem: Queries are slow or timing out**

**Cause:** Missing indexes or N+1 query problem.

**Solution:**
1. Check `docs/persistence.md` for recommended indexes
2. Use `.Include()` for eager loading:
   ```csharp
   var user = await context.Users
       .Include(u => u.Roles)
           .ThenInclude(ur => ur.Role)
               .ThenInclude(r => r.Permissions)
       .FirstOrDefaultAsync(u => u.Id == userId);
   ```
3. Enable EF Core query logging to diagnose:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Microsoft.EntityFrameworkCore.Database.Command": "Information"
       }
     }
   }
   ```

### Testing Issues

**Problem: Tests fail with "Database operation expected to affect 1 row(s) but actually affected 0"**

**Cause:** InMemory database doesn't enforce all constraints like a real database.

**Solution:**
1. Use unique database names per test:
   ```csharp
   var options = new DbContextOptionsBuilder<AppDbContext>()
       .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
       .Options;
   ```
2. For integration tests requiring full EF Core behavior, use SQLite instead:
   ```csharp
   .UseSqlite("DataSource=:memory:")
   ```

**Problem: Shouldly assertion failure messages unclear**

**Solution:**
Shouldly messages are designed to be readable. Example:
```
products.Count
    should be
10
    but was
5
```
If unclear, add custom message:
```csharp
products.Count.ShouldBe(10, "Expected 10 products after seeding");
```

### Build / SDK Issues

**Problem: `error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0`**

**Solution:**
Install .NET 10 Preview SDK: https://dotnet.microsoft.com/download/dotnet/10.0

**Problem: Package restore fails with `NU1202: Package X is not compatible with net10.0`**

**Solution:**
1. Ensure package supports .NET 10 (check `Directory.Packages.props`)
2. Update to RC/Preview versions if available
3. Check package documentation for compatibility

## Future SPECs Implementation Order

Per `docs/implementation-order-summary.md`, recommended order:

1. **SPEC-0009**: Structured Logging (no dependencies)
2. **SPEC-0003**: Problem Details (weak dependency on 0009)
3. **SPEC-0005**: Error Middleware (depends on 0009, 0003)
4. **SPEC-0002**: CORS Support (parallel)
5. **SPEC-0004**: Health Checks (parallel)
6. **SPEC-0008**: Rate Limiting (depends on 0005, 0009)
7. **SPEC-0010**: Metrics/Telemetry (parallel)
8. **SPEC-0007**: System Parameters (parallel)
9. **SPEC-0001**: User Registration Formalization (parallel)
