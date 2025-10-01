using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Configurations;

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Environment)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(256);

        builder.Property(x => x.IsCritical)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.LastUpdatedBy)
            .HasMaxLength(64);

        builder.Property(x => x.LastUpdatedAt)
            .IsRequired();

        builder.Property(x => x.Metadata)
            .HasColumnType("text");

        // Unique index on Key + Environment
        builder.HasIndex(x => new { x.Key, x.Environment })
            .IsUnique()
            .HasDatabaseName("IX_FeatureFlags_Key_Environment");

        // Index for querying by environment
        builder.HasIndex(x => x.Environment)
            .HasDatabaseName("IX_FeatureFlags_Environment");

        // Seed data - Section 19.1: Infrastructure flags (Critical)
        builder.HasData(
            // feature-flags-enabled (kill switch)
            CreateFlag("feature-flags-enabled", "Development", true, 
                "Habilita o próprio módulo de feature flags", isCritical: true),
            CreateFlag("feature-flags-enabled", "Staging", true, 
                "Habilita o próprio módulo de feature flags", isCritical: true),
            CreateFlag("feature-flags-enabled", "Production", true, 
                "Habilita o próprio módulo de feature flags", isCritical: true),
            
            // rbac-enabled (migrated from RbacOptions.FeatureEnabled)
            CreateFlag("rbac-enabled", "Development", true, 
                "Habilita sistema RBAC (migrado de RbacOptions.FeatureEnabled)", isCritical: true),
            CreateFlag("rbac-enabled", "Staging", true, 
                "Habilita sistema RBAC (migrado de RbacOptions.FeatureEnabled)", isCritical: true),
            CreateFlag("rbac-enabled", "Production", true, 
                "Habilita sistema RBAC (migrado de RbacOptions.FeatureEnabled)", isCritical: true),
            
            // Section 19.2: Planned features flags
            // SPEC-0001
            CreateFlag("user-registration-enabled", "Development", true,
                "Permite registro de novos usuários (SPEC-0001)"),
            CreateFlag("user-registration-enabled", "Staging", true,
                "Permite registro de novos usuários (SPEC-0001)"),
            CreateFlag("user-registration-enabled", "Production", true,
                "Permite registro de novos usuários (SPEC-0001)"),
            
            // SPEC-0002
            CreateFlag("cors-relaxed", "Development", true,
                "Habilita política CORS permissiva para dev/debug (SPEC-0002)"),
            CreateFlag("cors-relaxed", "Staging", false,
                "Habilita política CORS permissiva para dev/debug (SPEC-0002)"),
            CreateFlag("cors-relaxed", "Production", false,
                "Habilita política CORS permissiva para dev/debug (SPEC-0002)"),
            
            // SPEC-0003
            CreateFlag("problem-details-enabled", "Development", true,
                "Usa Problem Details (RFC 7807) em erros (SPEC-0003)"),
            CreateFlag("problem-details-enabled", "Staging", true,
                "Usa Problem Details (RFC 7807) em erros (SPEC-0003)"),
            CreateFlag("problem-details-enabled", "Production", false,
                "Usa Problem Details (RFC 7807) em erros (SPEC-0003)"),
            
            // SPEC-0004
            CreateFlag("health-checks-enabled", "Development", true,
                "Expõe endpoints de health check (SPEC-0004)"),
            CreateFlag("health-checks-enabled", "Staging", true,
                "Expõe endpoints de health check (SPEC-0004)"),
            CreateFlag("health-checks-enabled", "Production", true,
                "Expõe endpoints de health check (SPEC-0004)"),
            
            // SPEC-0005
            CreateFlag("error-middleware-enabled", "Development", true,
                "Ativa middleware global de tratamento de erros (SPEC-0005)"),
            CreateFlag("error-middleware-enabled", "Staging", true,
                "Ativa middleware global de tratamento de erros (SPEC-0005)"),
            CreateFlag("error-middleware-enabled", "Production", false,
                "Ativa middleware global de tratamento de erros (SPEC-0005)"),
            
            // SPEC-0007
            CreateFlag("system-params-enabled", "Development", true,
                "Habilita módulo de parâmetros do sistema (SPEC-0007)"),
            CreateFlag("system-params-enabled", "Staging", true,
                "Habilita módulo de parâmetros do sistema (SPEC-0007)"),
            CreateFlag("system-params-enabled", "Production", false,
                "Habilita módulo de parâmetros do sistema (SPEC-0007)"),
            
            // SPEC-0008
            CreateFlag("rate-limiting-enabled", "Development", false,
                "Ativa rate limiting global (SPEC-0008)"),
            CreateFlag("rate-limiting-enabled", "Staging", true,
                "Ativa rate limiting global (SPEC-0008)"),
            CreateFlag("rate-limiting-enabled", "Production", true,
                "Ativa rate limiting global (SPEC-0008)"),
            
            // SPEC-0009
            CreateFlag("structured-logging-enabled", "Development", true,
                "Usa Serilog com enriquecimento estruturado (SPEC-0009)"),
            CreateFlag("structured-logging-enabled", "Staging", true,
                "Usa Serilog com enriquecimento estruturado (SPEC-0009)"),
            CreateFlag("structured-logging-enabled", "Production", true,
                "Usa Serilog com enriquecimento estruturado (SPEC-0009)"),
            
            // SPEC-0010
            CreateFlag("metrics-enabled", "Development", true,
                "Exporta métricas Prometheus (SPEC-0010)"),
            CreateFlag("metrics-enabled", "Staging", true,
                "Exporta métricas Prometheus (SPEC-0010)"),
            CreateFlag("metrics-enabled", "Production", true,
                "Exporta métricas Prometheus (SPEC-0010)"),
            
            CreateFlag("metrics-detailed-auth", "Development", true,
                "Métricas detalhadas de autenticação (SPEC-0010)"),
            CreateFlag("metrics-detailed-auth", "Staging", false,
                "Métricas detalhadas de autenticação (SPEC-0010)"),
            CreateFlag("metrics-detailed-auth", "Production", false,
                "Métricas detalhadas de autenticação (SPEC-0010)"),
            
            // Section 19.3: Future flags (V2+)
            CreateFlag("feature-flags-audit-enabled", "Development", false,
                "Grava auditoria completa de alterações (SPEC-0006-V2)"),
            CreateFlag("feature-flags-audit-enabled", "Staging", false,
                "Grava auditoria completa de alterações (SPEC-0006-V2)"),
            CreateFlag("feature-flags-audit-enabled", "Production", false,
                "Grava auditoria completa de alterações (SPEC-0006-V2)")
        );
    }

    private static FeatureFlag CreateFlag(
        string key,
        string environment,
        bool isEnabled,
        string description,
        bool isCritical = false)
    {
        // Use deterministic GUID generation based on key and environment
        var guidBytes = System.Text.Encoding.UTF8.GetBytes($"{key}:{environment}");
        var hash = System.Security.Cryptography.SHA256.HashData(guidBytes);
        var guid = new Guid(hash.Take(16).ToArray());
        
        // Use fixed date for seed data
        var seedDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Use reflection to create the entity since the Create method validates kebab-case
        var entity = (FeatureFlag)Activator.CreateInstance(typeof(FeatureFlag), true)!;
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.Id))!.SetValue(entity, guid);
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.Key))!.SetValue(entity, key.ToLowerInvariant());
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.Environment))!.SetValue(entity, environment);
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.IsEnabled))!.SetValue(entity, isEnabled);
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.Description))!.SetValue(entity, description);
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.IsCritical))!.SetValue(entity, isCritical);
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.LastUpdatedBy))!.SetValue(entity, "system-seed");
        typeof(FeatureFlag).GetProperty(nameof(FeatureFlag.LastUpdatedAt))!.SetValue(entity, seedDate);
        
        return entity;
    }
}
