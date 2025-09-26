# Parte 4: Entity Framework e Configuração

## 📋 Visão Geral
**Duração**: 20-30 minutos  
**Complexidade**: Média  
**Dependências**: Partes 1, 2 e 3 (Setup + Entidades)

Esta parte configura o Entity Framework Core com PostgreSQL, criando o DbContext completo, configurações de entidades, migrations e dados iniciais (seed data).

## 🎯 Objetivos
- ✅ Configurar ApplicationDbContext completo
- ✅ Criar configurações específicas para cada entidade
- ✅ Implementar relacionamentos e índices
- ✅ Criar migrations iniciais
- ✅ Implementar seed data para configurações padrão
- ✅ Configurar connection strings

## 📁 Arquivos a serem Criados

```
src/IDE.Infrastructure/Data/
├── ApplicationDbContext.cs
├── Configurations/
│   ├── UserConfiguration.cs
│   ├── RefreshTokenConfiguration.cs
│   ├── ApiKeyConfiguration.cs
│   ├── UserLoginHistoryConfiguration.cs
│   ├── EmailTemplateConfiguration.cs
│   ├── SystemConfigurationConfiguration.cs
│   ├── PlanLimitsConfiguration.cs
│   └── SecurityConfigurationConfiguration.cs
└── Seeds/
    ├── DefaultConfigurationSeed.cs
    ├── EmailTemplateSeed.cs
    └── PlanLimitsSeed.cs
```

## 🚀 Execução Passo a Passo

### 1. Configurar ApplicationDbContext

#### src/IDE.Infrastructure/Data/ApplicationDbContext.cs
```csharp
using Microsoft.EntityFrameworkCore;
using IDE.Domain.Entities;
using IDE.Infrastructure.Data.Configurations;
using IDE.Infrastructure.Data.Seeds;

namespace IDE.Infrastructure.Data;

/// <summary>
/// Contexto principal do Entity Framework para a aplicação
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSets para todas as entidades
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<ApiKey> ApiKeys { get; set; } = null!;
    public DbSet<UserLoginHistory> UserLoginHistory { get; set; } = null!;
    public DbSet<EmailTemplate> EmailTemplates { get; set; } = null!;
    public DbSet<SystemConfiguration> SystemConfigurations { get; set; } = null!;
    public DbSet<PlanLimits> PlanLimits { get; set; } = null!;
    public DbSet<SecurityConfiguration> SecurityConfigurations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas as configurações
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());
        modelBuilder.ApplyConfiguration(new UserLoginHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new EmailTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new SystemConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new PlanLimitsConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityConfigurationConfiguration());

        // Aplicar seeds
        DefaultConfigurationSeed.Seed(modelBuilder);
        EmailTemplateSeed.Seed(modelBuilder);
        PlanLimitsSeed.Seed(modelBuilder);

        // Configurações globais
        ConfigureGlobalSettings(modelBuilder);
    }

    /// <summary>
    /// Configurações globais para todas as entidades
    /// </summary>
    private static void ConfigureGlobalSettings(ModelBuilder modelBuilder)
    {
        // Configurar precisão de decimais para PostgreSQL
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        // Configurar strings para usar UTF-8
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(string)))
        {
            property.SetCollation("C");
        }

        // Configurar nomes de tabelas em snake_case para PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()?.ToSnakeCase());

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToSnakeCase());
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(key.GetName()?.ToSnakeCase());
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(foreignKey.GetConstraintName()?.ToSnakeCase());
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(index.GetDatabaseName()?.ToSnakeCase());
            }
        }
    }

    /// <summary>
    /// Override SaveChanges para adicionar timestamp automático
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Override SaveChanges síncrono
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// Atualiza timestamps automaticamente
    /// </summary>
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is User user)
            {
                if (entry.State == EntityState.Modified)
                {
                    // Não atualizar CreatedAt em modificações
                    entry.Property(nameof(User.CreatedAt)).IsModified = false;
                }
            }

            // Atualizar UpdatedAt para entidades que têm essa propriedade
            if (entry.Entity.GetType().GetProperty("UpdatedAt") != null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}

/// <summary>
/// Extensão para converter strings para snake_case
/// </summary>
public static class StringExtensions
{
    public static string ToSnakeCase(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = System.Text.RegularExpressions.Regex.Replace(
            input, 
            "(?<!^)([A-Z])", 
            "_$1"
        ).ToLowerInvariant();

        return result;
    }
}
```

### 2. Criar Configurações de Entidades

#### src/IDE.Infrastructure/Data/Configurations/UserConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Configurações de tabela
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        // Configurações de propriedades
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(u => u.Username)
            .HasColumnName("username")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Avatar)
            .HasColumnName("avatar")
            .HasMaxLength(500);

        builder.Property(u => u.AvatarProvider)
            .HasColumnName("avatar_provider")
            .HasMaxLength(20)
            .HasDefaultValue("Default");

        builder.Property(u => u.EmailVerified)
            .HasColumnName("email_verified")
            .HasDefaultValue(false);

        builder.Property(u => u.EmailVerifiedAt)
            .HasColumnName("email_verified_at");

        builder.Property(u => u.EmailVerificationToken)
            .HasColumnName("email_verification_token")
            .HasMaxLength(255);

        builder.Property(u => u.EmailVerificationTokenExpiresAt)
            .HasColumnName("email_verification_token_expires_at");

        builder.Property(u => u.PasswordResetToken)
            .HasColumnName("password_reset_token")
            .HasMaxLength(255);

        builder.Property(u => u.PasswordResetTokenExpiresAt)
            .HasColumnName("password_reset_token_expires_at");

        builder.Property(u => u.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts")
            .HasDefaultValue(0);

        builder.Property(u => u.LockedOutUntil)
            .HasColumnName("locked_out_until");

        builder.Property(u => u.TwoFactorEnabled)
            .HasColumnName("two_factor_enabled")
            .HasDefaultValue(false);

        builder.Property(u => u.TwoFactorSecret)
            .HasColumnName("two_factor_secret")
            .HasMaxLength(255);

        builder.Property(u => u.TwoFactorMethod)
            .HasColumnName("two_factor_method")
            .HasConversion<int>()
            .HasDefaultValue(0);

        builder.Property(u => u.Plan)
            .HasColumnName("plan")
            .HasConversion<int>()
            .HasDefaultValue(0);

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(u => u.LastLoginIp)
            .HasColumnName("last_login_ip")
            .HasMaxLength(45);

        builder.Property(u => u.LastLoginUserAgent)
            .HasColumnName("last_login_user_agent")
            .HasMaxLength(500);

        // Índices únicos
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("ix_users_username");

        // Índices para performance
        builder.HasIndex(u => u.EmailVerificationToken)
            .HasDatabaseName("ix_users_email_verification_token");

        builder.HasIndex(u => u.PasswordResetToken)
            .HasDatabaseName("ix_users_password_reset_token");

        builder.HasIndex(u => u.Plan)
            .HasDatabaseName("ix_users_plan");

        builder.HasIndex(u => u.CreatedAt)
            .HasDatabaseName("ix_users_created_at");

        // Relacionamentos
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.ApiKeys)
            .WithOne(ak => ak.User)
            .HasForeignKey(ak => ak.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.LoginHistory)
            .WithOne(lh => lh.User)
            .HasForeignKey(lh => lh.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(rt => rt.Token)
            .HasColumnName("token")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(rt => rt.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false);

        builder.Property(rt => rt.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(rt => rt.DeviceInfo)
            .HasColumnName("device_info")
            .HasMaxLength(500);

        builder.Property(rt => rt.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(rt => rt.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(rt => rt.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(rt => rt.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(rt => rt.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        // Índices
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token");

        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("ix_refresh_tokens_user_id");

        builder.HasIndex(rt => rt.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");

        builder.HasIndex(rt => new { rt.UserId, rt.IsRevoked })
            .HasDatabaseName("ix_refresh_tokens_user_revoked");
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/ApiKeyConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.HasKey(ak => ak.Id);

        builder.Property(ak => ak.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(ak => ak.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ak => ak.Key)
            .HasColumnName("key")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ak => ak.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ak => ak.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(ak => ak.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(ak => ak.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(ak => ak.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(ak => ak.LastUsedIp)
            .HasColumnName("last_used_ip")
            .HasMaxLength(45);

        builder.Property(ak => ak.UsageCount)
            .HasColumnName("usage_count")
            .HasDefaultValue(0);

        builder.Property(ak => ak.Scopes)
            .HasColumnName("scopes")
            .HasMaxLength(1000)
            .HasDefaultValue("[]");

        builder.Property(ak => ak.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        // Índices
        builder.HasIndex(ak => ak.Key)
            .IsUnique()
            .HasDatabaseName("ix_api_keys_key");

        builder.HasIndex(ak => ak.UserId)
            .HasDatabaseName("ix_api_keys_user_id");

        builder.HasIndex(ak => new { ak.UserId, ak.IsActive })
            .HasDatabaseName("ix_api_keys_user_active");

        builder.HasIndex(ak => ak.ExpiresAt)
            .HasDatabaseName("ix_api_keys_expires_at");
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/UserLoginHistoryConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class UserLoginHistoryConfiguration : IEntityTypeConfiguration<UserLoginHistory>
{
    public void Configure(EntityTypeBuilder<UserLoginHistory> builder)
    {
        builder.ToTable("user_login_history");
        builder.HasKey(lh => lh.Id);

        builder.Property(lh => lh.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(lh => lh.LoginAt)
            .HasColumnName("login_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(lh => lh.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(lh => lh.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(lh => lh.Country)
            .HasColumnName("country")
            .HasMaxLength(100);

        builder.Property(lh => lh.City)
            .HasColumnName("city")
            .HasMaxLength(100);

        builder.Property(lh => lh.IsSuccess)
            .HasColumnName("is_success")
            .IsRequired();

        builder.Property(lh => lh.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(255);

        builder.Property(lh => lh.LoginMethod)
            .HasColumnName("login_method")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(lh => lh.DeviceInfo)
            .HasColumnName("device_info")
            .HasMaxLength(500);

        builder.Property(lh => lh.IsSuspicious)
            .HasColumnName("is_suspicious")
            .HasDefaultValue(false);

        builder.Property(lh => lh.LoginDurationMs)
            .HasColumnName("login_duration_ms")
            .HasDefaultValue(0);

        builder.Property(lh => lh.UserId)
            .HasColumnName("user_id");

        builder.Property(lh => lh.IsFromNewLocation)
            .HasColumnName("is_from_new_location")
            .HasDefaultValue(false);

        builder.Property(lh => lh.RiskScore)
            .HasColumnName("risk_score")
            .HasDefaultValue(0);

        // Índices
        builder.HasIndex(lh => lh.UserId)
            .HasDatabaseName("ix_login_history_user_id");

        builder.HasIndex(lh => lh.LoginAt)
            .HasDatabaseName("ix_login_history_login_at");

        builder.HasIndex(lh => lh.IpAddress)
            .HasDatabaseName("ix_login_history_ip_address");

        builder.HasIndex(lh => new { lh.UserId, lh.IsSuccess })
            .HasDatabaseName("ix_login_history_user_success");

        builder.HasIndex(lh => lh.IsSuspicious)
            .HasDatabaseName("ix_login_history_suspicious");
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/EmailTemplateConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates");
        builder.HasKey(et => et.Id);

        builder.Property(et => et.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(et => et.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(et => et.Subject)
            .HasColumnName("subject")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(et => et.HtmlBody)
            .HasColumnName("html_body")
            .IsRequired();

        builder.Property(et => et.TextBody)
            .HasColumnName("text_body");

        builder.Property(et => et.Language)
            .HasColumnName("language")
            .HasMaxLength(10)
            .HasDefaultValue("pt-BR");

        builder.Property(et => et.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(et => et.Category)
            .HasColumnName("category")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(et => et.Priority)
            .HasColumnName("priority")
            .HasDefaultValue(3);

        builder.Property(et => et.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(et => et.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(et => et.Version)
            .HasColumnName("version")
            .HasDefaultValue(1);

        builder.Property(et => et.AvailableVariables)
            .HasColumnName("available_variables")
            .HasDefaultValue("[]");

        // Índices
        builder.HasIndex(et => new { et.Name, et.Language })
            .IsUnique()
            .HasDatabaseName("ix_email_templates_name_language");

        builder.HasIndex(et => et.Category)
            .HasDatabaseName("ix_email_templates_category");

        builder.HasIndex(et => et.IsActive)
            .HasDatabaseName("ix_email_templates_active");
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/SystemConfigurationConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.ToTable("system_configurations");
        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(sc => sc.Key)
            .HasColumnName("key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(sc => sc.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.Property(sc => sc.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(sc => sc.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .HasDefaultValue(0);

        builder.Property(sc => sc.Category)
            .HasColumnName("category")
            .HasMaxLength(50)
            .HasDefaultValue("General");

        builder.Property(sc => sc.IsSensitive)
            .HasColumnName("is_sensitive")
            .HasDefaultValue(false);

        builder.Property(sc => sc.IsReadOnly)
            .HasColumnName("is_read_only")
            .HasDefaultValue(false);

        builder.Property(sc => sc.DefaultValue)
            .HasColumnName("default_value");

        builder.Property(sc => sc.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(sc => sc.UpdatedBy)
            .HasColumnName("updated_by")
            .HasDefaultValue("System");

        // Índices
        builder.HasIndex(sc => sc.Key)
            .IsUnique()
            .HasDatabaseName("ix_system_configurations_key");

        builder.HasIndex(sc => sc.Category)
            .HasDatabaseName("ix_system_configurations_category");

        builder.HasIndex(sc => sc.Type)
            .HasDatabaseName("ix_system_configurations_type");
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/PlanLimitsConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class PlanLimitsConfiguration : IEntityTypeConfiguration<PlanLimits>
{
    public void Configure(EntityTypeBuilder<PlanLimits> builder)
    {
        builder.ToTable("plan_limits");
        builder.HasKey(pl => pl.Id);

        builder.Property(pl => pl.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(pl => pl.Plan)
            .HasColumnName("plan")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(pl => pl.MaxWorkspaces)
            .HasColumnName("max_workspaces")
            .HasDefaultValue(10);

        builder.Property(pl => pl.MaxStoragePerWorkspace)
            .HasColumnName("max_storage_per_workspace")
            .HasDefaultValue(10 * 1024 * 1024);

        builder.Property(pl => pl.MaxItemSize)
            .HasColumnName("max_item_size")
            .HasDefaultValue(5 * 1024 * 1024);

        builder.Property(pl => pl.MaxCollaboratorsPerWorkspace)
            .HasColumnName("max_collaborators_per_workspace")
            .HasDefaultValue(5);

        builder.Property(pl => pl.CanUseApiKeys)
            .HasColumnName("can_use_api_keys")
            .HasDefaultValue(false);

        builder.Property(pl => pl.CanExportWorkspaces)
            .HasColumnName("can_export_workspaces")
            .HasDefaultValue(false);

        builder.Property(pl => pl.CanSharePublicly)
            .HasColumnName("can_share_publicly")
            .HasDefaultValue(false);

        builder.Property(pl => pl.MaxApiKeys)
            .HasColumnName("max_api_keys")
            .HasDefaultValue(0);

        builder.Property(pl => pl.RateLimitPerMinute)
            .HasColumnName("rate_limit_per_minute")
            .HasDefaultValue(1000);

        builder.Property(pl => pl.CanIntegrateThirdParty)
            .HasColumnName("can_integrate_third_party")
            .HasDefaultValue(false);

        builder.Property(pl => pl.HasPrioritySupport)
            .HasColumnName("has_priority_support")
            .HasDefaultValue(false);

        builder.Property(pl => pl.HasAutomaticBackup)
            .HasColumnName("has_automatic_backup")
            .HasDefaultValue(false);

        builder.Property(pl => pl.VersionHistoryDays)
            .HasColumnName("version_history_days")
            .HasDefaultValue(7);

        builder.Property(pl => pl.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(pl => pl.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Índices
        builder.HasIndex(pl => pl.Plan)
            .IsUnique()
            .HasDatabaseName("ix_plan_limits_plan");
    }
}
```

#### src/IDE.Infrastructure/Data/Configurations/SecurityConfigurationConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Configurations;

public class SecurityConfigurationConfiguration : IEntityTypeConfiguration<SecurityConfiguration>
{
    public void Configure(EntityTypeBuilder<SecurityConfiguration> builder)
    {
        builder.ToTable("security_configurations");
        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(sc => sc.MaxFailedAttempts)
            .HasColumnName("max_failed_attempts")
            .HasDefaultValue(5);

        builder.Property(sc => sc.LockoutDurationMinutes)
            .HasColumnName("lockout_duration_minutes")
            .HasDefaultValue(15);

        builder.Property(sc => sc.LockoutIncrement)
            .HasColumnName("lockout_increment")
            .HasDefaultValue(true);

        builder.Property(sc => sc.ResetFailedAttemptsAfterHours)
            .HasColumnName("reset_failed_attempts_after_hours")
            .HasDefaultValue(24);

        builder.Property(sc => sc.PasswordResetTokenExpirationMinutes)
            .HasColumnName("password_reset_token_expiration_minutes")
            .HasDefaultValue(30);

        builder.Property(sc => sc.EmailVerificationTokenExpirationHours)
            .HasColumnName("email_verification_token_expiration_hours")
            .HasDefaultValue(24);

        builder.Property(sc => sc.ResendCooldownMinutes)
            .HasColumnName("resend_cooldown_minutes")
            .HasDefaultValue(5);

        builder.Property(sc => sc.MaxResendAttempts)
            .HasColumnName("max_resend_attempts")
            .HasDefaultValue(3);

        builder.Property(sc => sc.RequireVerificationForLogin)
            .HasColumnName("require_verification_for_login")
            .HasDefaultValue(false);

        builder.Property(sc => sc.ForceLogoutOnPasswordChange)
            .HasColumnName("force_logout_on_password_change")
            .HasDefaultValue(true);

        builder.Property(sc => sc.NotifyNewDeviceLogin)
            .HasColumnName("notify_new_device_login")
            .HasDefaultValue(true);

        builder.Property(sc => sc.JwtExpirationMinutes)
            .HasColumnName("jwt_expiration_minutes")
            .HasDefaultValue(60);

        builder.Property(sc => sc.RefreshTokenExpirationDays)
            .HasColumnName("refresh_token_expiration_days")
            .HasDefaultValue(7);

        builder.Property(sc => sc.AllowMultipleRefreshTokens)
            .HasColumnName("allow_multiple_refresh_tokens")
            .HasDefaultValue(true);

        builder.Property(sc => sc.MaxRefreshTokensPerUser)
            .HasColumnName("max_refresh_tokens_per_user")
            .HasDefaultValue(5);

        builder.Property(sc => sc.EnableLoginAudit)
            .HasColumnName("enable_login_audit")
            .HasDefaultValue(true);

        builder.Property(sc => sc.AuditLogRetentionDays)
            .HasColumnName("audit_log_retention_days")
            .HasDefaultValue(90);

        builder.Property(sc => sc.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(sc => sc.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(sc => sc.UpdatedBy)
            .HasColumnName("updated_by")
            .HasDefaultValue("System");
    }
}
```

### 3. Criar Seeds de Dados

#### src/IDE.Infrastructure/Data/Seeds/DefaultConfigurationSeed.cs
```csharp
using Microsoft.EntityFrameworkCore;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Seeds;

public static class DefaultConfigurationSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var configs = SystemConfiguration.CreateDefaults();
        
        modelBuilder.Entity<SystemConfiguration>().HasData(configs);
    }
}
```

#### src/IDE.Infrastructure/Data/Seeds/EmailTemplateSeed.cs
```csharp
using Microsoft.EntityFrameworkCore;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Seeds;

public static class EmailTemplateSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var templates = new List<EmailTemplate>
        {
            EmailTemplate.CreateWelcomeTemplate(),
            EmailTemplate.CreateEmailVerificationTemplate(),
            EmailTemplate.CreatePasswordResetTemplate()
        };

        modelBuilder.Entity<EmailTemplate>().HasData(templates);
    }
}
```

#### src/IDE.Infrastructure/Data/Seeds/PlanLimitsSeed.cs
```csharp
using Microsoft.EntityFrameworkCore;
using IDE.Domain.Entities;

namespace IDE.Infrastructure.Data.Seeds;

public static class PlanLimitsSeed
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        var planLimits = PlanLimits.CreateDefaults();
        
        modelBuilder.Entity<PlanLimits>().HasData(planLimits);

        // Seed configuração de segurança padrão
        var securityConfig = SecurityConfiguration.CreateDefault();
        modelBuilder.Entity<SecurityConfiguration>().HasData(securityConfig);
    }
}
```

### 4. Configurar Connection String

#### src/IDE.API/appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ide_backend;Username=postgres;Password=postgres;Include Error Detail=true",
    "Redis": "localhost:6379"
  },
  "JWT": {
    "Secret": "your-super-secret-jwt-key-that-is-at-least-256-bits-long",
    "Issuer": "IDE.API",
    "Audience": "IDE.Frontend",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "Email": {
    "Provider": "Mock",
    "SendGrid": {
      "ApiKey": "your-sendgrid-api-key",
      "FromEmail": "noreply@yourdomain.com",
      "FromName": "IDE Colaborativo"
    },
    "Gmail": {
      "SmtpServer": "smtp.gmail.com",
      "Port": 587,
      "Username": "your-gmail@gmail.com",
      "Password": "your-app-password",
      "EnableSsl": true
    },
    "Outlook": {
      "SmtpServer": "smtp-mail.outlook.com",
      "Port": 587,
      "Username": "your-outlook@outlook.com",
      "Password": "your-password",
      "EnableSsl": true
    },
    "Smtp": {
      "SmtpServer": "your-smtp-server.com",
      "Port": 587,
      "Username": "your-username",
      "Password": "your-password",
      "EnableSsl": true
    }
  }
}
```

#### src/IDE.API/appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ide_backend_dev;Username=postgres;Password=postgres;Include Error Detail=true",
    "Redis": "localhost:6379"
  },
  "JWT": {
    "Secret": "development-jwt-secret-key-that-is-at-least-256-bits-long",
    "ExpirationMinutes": 120
  },
  "Email": {
    "Provider": "Mock"
  }
}
```

### 5. Criar e Aplicar Migrations

Execute os comandos para criar e aplicar as migrations:

```powershell
# Na raiz do projeto
cd src\IDE.API

# Criar migration inicial
dotnet ef migrations add InitialCreate --project ..\IDE.Infrastructure

# Aplicar migration
dotnet ef database update --project ..\IDE.Infrastructure

# Voltar para a raiz
cd ..\..
```

### 6. Validar Implementação

```powershell
# Restaurar dependências
dotnet restore

# Compilar
dotnet build

# Testar conexão com banco (se PostgreSQL estiver rodando)
dotnet ef database update --project src\IDE.Infrastructure --startup-project src\IDE.API
```

## ✅ Critérios de Validação

Ao final desta parte, você deve ter:

- [ ] **ApplicationDbContext** configurado com todas as entidades
- [ ] **Configurações EF** completas para todas as entidades
- [ ] **Migrations** criadas e aplicadas com sucesso
- [ ] **Seed data** implementado para configurações padrão
- [ ] **Índices** criados para performance
- [ ] **Relacionamentos** funcionando corretamente
- [ ] **Connection strings** configuradas
- [ ] **Compilação bem-sucedida** sem erros

## 📝 Arquivos Criados

Esta parte criará aproximadamente **15 arquivos**:
- 1 ApplicationDbContext
- 8 Configurations
- 3 Seeds
- 2 appsettings
- 1+ Migration files

## 🔄 Próximos Passos

Após concluir esta parte, você estará pronto para:
- **Parte 5**: DTOs e Requests de Autenticação
- Implementar todos os DTOs para comunicação da API
- Configurar AutoMapper profiles

## 🚨 Troubleshooting Comum

**Erro de conexão**: Verifique se PostgreSQL está rodando na porta 5432  
**Migration failed**: Verifique se a string de conexão está correta  
**Seed data error**: Verifique se as entidades estão bem configuradas  

---
**⏱️ Tempo estimado**: 20-30 minutos  
**🎯 Próxima parte**: 05-dtos-requests-autenticacao.md  
**📋 Dependências**: Partes 1, 2 e 3 concluídas