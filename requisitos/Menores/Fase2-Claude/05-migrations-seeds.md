# Parte 5: Migrations e Seeds - Workspace Core

## Contexto
Esta é a **Parte 5 de 12** da Fase 2 (Workspace Core). Aqui criaremos as migrations do Entity Framework e configuraremos os dados iniciais (seeds) necessários para o funcionamento do sistema.

**Pré-requisitos**: Parte 4 (DbContext Configuration) deve estar concluída

**Dependências**: ApplicationDbContext configurado com todas as entidades

**Próxima parte**: Parte 6 - DTOs Básicos

## Objetivos desta Parte
✅ Criar migration inicial da Fase 2  
✅ Configurar seed data para SystemParameters  
✅ Criar scripts de manutenção e limpeza  
✅ Configurar dados padrão do sistema  
✅ Preparar ambiente para desenvolvimento  

## 1. Migration da Fase 2

### 1.1 Comandos para Criar Migration

#### Terminal Commands
```bash
# Navegar para o projeto Infrastructure
cd IDE.Infrastructure

# Criar a migration da Fase 2
dotnet ef migrations add "Fase2_WorkspaceCore" --context ApplicationDbContext --startup-project ../IDE.API

# Verificar a migration criada
dotnet ef migrations list --context ApplicationDbContext --startup-project ../IDE.API

# Aplicar migration (em desenvolvimento)
dotnet ef database update --context ApplicationDbContext --startup-project ../IDE.API
```

### 1.2 Migration File Structure

A migration criada terá aproximadamente esta estrutura:

#### IDE.Infrastructure/Migrations/[Timestamp]_Fase2_WorkspaceCore.cs
```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IDE.Infrastructure.Migrations
{
    public partial class Fase2_WorkspaceCore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Criar tabelas principais
            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SemanticVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "1.0.0"),
                    CurrentPhaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspaces_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Criar tabela WorkspacePhases
            migrationBuilder.CreateTable(
                name: "WorkspacePhases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, defaultValue: "#52c41a"),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspacePhases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspacePhases_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ... (demais tabelas seguem o mesmo padrão)
            
            // Adicionar foreign key do CurrentPhaseId após criar WorkspacePhases
            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_WorkspacePhases_CurrentPhaseId",
                table: "Workspaces",
                column: "CurrentPhaseId",
                principalTable: "WorkspacePhases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Criar índices de performance
            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Owner_Name",
                table: "Workspaces",
                columns: new[] { "OwnerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_CreatedAt",
                table: "Workspaces",
                column: "CreatedAt");

            // ... (demais índices)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop em ordem reversa devido às foreign keys
            migrationBuilder.DropTable("ItemVersions");
            migrationBuilder.DropTable("WorkspaceVersions");
            migrationBuilder.DropTable("WorkspaceNavigationStates");
            migrationBuilder.DropTable("ModuleItemTags");
            migrationBuilder.DropTable("WorkspaceTags");
            migrationBuilder.DropTable("ActivityLogs");
            migrationBuilder.DropTable("WorkspaceInvitations");
            migrationBuilder.DropTable("WorkspacePermissions");
            migrationBuilder.DropTable("ModuleItems");
            migrationBuilder.DropTable("WorkspacePhases");
            migrationBuilder.DropTable("SystemParameters");
            migrationBuilder.DropTable("Workspaces");
        }
    }
}
```

## 2. Seeds de Dados do Sistema

### 2.1 SystemParameters Seed Data

#### IDE.Infrastructure/Data/ApplicationDbContext.cs (SeedWorkspaceData)
```csharp
private void SeedWorkspaceData(ModelBuilder modelBuilder)
{
    var systemParameters = new List<SystemParameter>
    {
        // QUOTAS - Limites por Plano
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Key = "MAX_WORKSPACES_FREE",
            Value = "1",
            Description = "Máximo de workspaces permitidos no plano Free",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "1",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Key = "MAX_WORKSPACES_PRO",
            Value = "5",
            Description = "Máximo de workspaces permitidos no plano Pro",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "5",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Key = "MAX_WORKSPACES_ENTERPRISE",
            Value = "15",
            Description = "Máximo de workspaces permitidos no plano Enterprise",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "15",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Key = "MAX_USERS_PER_WORKSPACE_FREE",
            Value = "2",
            Description = "Máximo de usuários por workspace no plano Free",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "2",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            Key = "MAX_USERS_PER_WORKSPACE_PRO",
            Value = "10",
            Description = "Máximo de usuários por workspace no plano Pro",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "10",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
            Key = "MAX_USERS_PER_WORKSPACE_ENTERPRISE",
            Value = "25",
            Description = "Máximo de usuários por workspace no plano Enterprise",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "25",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
            Key = "MAX_ITEMS_PER_WORKSPACE_FREE",
            Value = "100",
            Description = "Máximo de items por workspace no plano Free",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "100",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000008"),
            Key = "MAX_ITEMS_PER_WORKSPACE_PRO",
            Value = "1000",
            Description = "Máximo de items por workspace no plano Pro",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "1000",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000009"),
            Key = "MAX_ITEMS_PER_WORKSPACE_ENTERPRISE",
            Value = "5000",
            Description = "Máximo de items por workspace no plano Enterprise",
            Category = "QUOTAS",
            DataType = "INTEGER",
            DefaultValue = "5000",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },

        // RETENTION - Retenção de Dados
        new SystemParameter
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Key = "LOG_RETENTION_FREE_DAYS",
            Value = "7",
            Description = "Dias de retenção de activity logs no plano Free",
            Category = "RETENTION",
            DataType = "INTEGER",
            DefaultValue = "7",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
            Key = "LOG_RETENTION_PRO_DAYS",
            Value = "30",
            Description = "Dias de retenção de activity logs no plano Pro",
            Category = "RETENTION",
            DataType = "INTEGER",
            DefaultValue = "30",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
            Key = "LOG_RETENTION_ENTERPRISE_DAYS",
            Value = "90",
            Description = "Dias de retenção de activity logs no plano Enterprise",
            Category = "RETENTION",
            DataType = "INTEGER",
            DefaultValue = "90",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000004"),
            Key = "CACHE_RETENTION_HOURS",
            Value = "24",
            Description = "Horas de retenção do cache Redis",
            Category = "RETENTION",
            DataType = "INTEGER",
            DefaultValue = "24",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000005"),
            Key = "INVITATION_EXPIRY_DAYS",
            Value = "7",
            Description = "Dias para expiração de convites",
            Category = "RETENTION",
            DataType = "INTEGER",
            DefaultValue = "7",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },

        // FEATURES - Funcionalidades
        new SystemParameter
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Key = "ENABLE_REAL_TIME_SYNC",
            Value = "true",
            Description = "Habilitar sincronização em tempo real via SignalR",
            Category = "FEATURES",
            DataType = "BOOLEAN",
            DefaultValue = "true",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
            Key = "ENABLE_VERSION_HISTORY",
            Value = "true",
            Description = "Habilitar histórico de versões de items",
            Category = "FEATURES",
            DataType = "BOOLEAN",
            DefaultValue = "true",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000003"),
            Key = "ENABLE_ANALYTICS_PRO",
            Value = "false",
            Description = "Habilitar analytics no plano Pro",
            Category = "FEATURES",
            DataType = "BOOLEAN",
            DefaultValue = "false",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000004"),
            Key = "ENABLE_ANALYTICS_ENTERPRISE",
            Value = "true",
            Description = "Habilitar analytics no plano Enterprise",
            Category = "FEATURES",
            DataType = "BOOLEAN",
            DefaultValue = "true",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000005"),
            Key = "MAX_FILE_SIZE_MB",
            Value = "10",
            Description = "Tamanho máximo de arquivo em MB",
            Category = "FEATURES",
            DataType = "INTEGER",
            DefaultValue = "10",
            IsReadOnly = false,
            Environment = "ALL",
            MinValue = 1,
            MaxValue = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },

        // CACHE - Configurações de Cache
        new SystemParameter
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Key = "CACHE_WORKSPACE_TTL_MINUTES",
            Value = "30",
            Description = "TTL do cache de workspaces em minutos",
            Category = "CACHE",
            DataType = "INTEGER",
            DefaultValue = "30",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000002"),
            Key = "CACHE_ITEMS_TTL_MINUTES",
            Value = "15",
            Description = "TTL do cache de items em minutos",
            Category = "CACHE",
            DataType = "INTEGER",
            DefaultValue = "15",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000003"),
            Key = "CACHE_NAVIGATION_TTL_MINUTES",
            Value = "60",
            Description = "TTL do cache de navigation states em minutos",
            Category = "CACHE",
            DataType = "INTEGER",
            DefaultValue = "60",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000004"),
            Key = "REDIS_KEY_PREFIX",
            Value = "ide_workspace",
            Description = "Prefixo das chaves Redis",
            Category = "CACHE",
            DataType = "STRING",
            DefaultValue = "ide_workspace",
            IsReadOnly = true,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },

        // PERFORMANCE - Configurações de Performance
        new SystemParameter
        {
            Id = Guid.Parse("50000000-0000-0000-0000-000000000001"),
            Key = "MAX_NAVIGATION_STATE_SIZE_KB",
            Value = "100",
            Description = "Tamanho máximo do estado de navegação em KB",
            Category = "PERFORMANCE",
            DataType = "INTEGER",
            DefaultValue = "100",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("50000000-0000-0000-0000-000000000002"),
            Key = "ENABLE_NAVIGATION_STATE_COMPRESSION",
            Value = "true",
            Description = "Habilitar compressão do estado de navegação",
            Category = "PERFORMANCE",
            DataType = "BOOLEAN",
            DefaultValue = "true",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new SystemParameter
        {
            Id = Guid.Parse("50000000-0000-0000-0000-000000000003"),
            Key = "MAX_ACTIVITY_LOGS_PER_REQUEST",
            Value = "50",
            Description = "Máximo de activity logs retornados por request",
            Category = "PERFORMANCE",
            DataType = "INTEGER",
            DefaultValue = "50",
            IsReadOnly = false,
            Environment = "ALL",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };

    // Aplicar seed data
    modelBuilder.Entity<SystemParameter>().HasData(systemParameters);
}
```

## 3. Scripts de Manutenção

### 3.1 Script de Limpeza de Activity Logs

#### IDE.Infrastructure/Scripts/CleanupActivityLogs.sql
```sql
-- Script para limpeza automática de Activity Logs baseado na retenção por plano
-- Deve ser executado como job scheduled

-- Função para limpeza baseada nos SystemParameters
CREATE OR REPLACE FUNCTION cleanup_activity_logs()
RETURNS INTEGER AS $$
DECLARE
    free_retention INTEGER;
    pro_retention INTEGER;
    enterprise_retention INTEGER;
    deleted_count INTEGER := 0;
BEGIN
    -- Obter valores de retenção dos SystemParameters
    SELECT 
        MAX(CASE WHEN sp."Key" = 'LOG_RETENTION_FREE_DAYS' THEN sp."Value"::INTEGER END),
        MAX(CASE WHEN sp."Key" = 'LOG_RETENTION_PRO_DAYS' THEN sp."Value"::INTEGER END),
        MAX(CASE WHEN sp."Key" = 'LOG_RETENTION_ENTERPRISE_DAYS' THEN sp."Value"::INTEGER END)
    INTO free_retention, pro_retention, enterprise_retention
    FROM "SystemParameters" sp
    WHERE sp."Key" IN ('LOG_RETENTION_FREE_DAYS', 'LOG_RETENTION_PRO_DAYS', 'LOG_RETENTION_ENTERPRISE_DAYS');

    -- Limpeza para usuários Free (assumindo que User.Plan existe)
    DELETE FROM "ActivityLogs" al
    WHERE al."CreatedAt" < NOW() - INTERVAL '1 day' * free_retention
    AND al."WorkspaceId" IN (
        SELECT w."Id" 
        FROM "Workspaces" w 
        INNER JOIN "AspNetUsers" u ON w."OwnerId" = u."Id"
        WHERE u."Plan" = 'Free' -- Assumindo campo Plan na tabela User
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    
    -- Similar para Pro e Enterprise...
    
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;
```

### 3.2 Script de Limpeza de Convites Expirados

#### IDE.Infrastructure/Scripts/CleanupExpiredInvitations.sql
```sql
-- Script para limpeza de convites expirados
CREATE OR REPLACE FUNCTION cleanup_expired_invitations()
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER := 0;
BEGIN
    -- Marcar convites expirados
    UPDATE "WorkspaceInvitations"
    SET "Status" = 3, -- Expired
        "UpdatedAt" = NOW()
    WHERE "Status" = 0 -- Pending
    AND "ExpiresAt" < NOW();
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    
    -- Opcionalmente deletar convites expirados após X dias
    -- DELETE FROM "WorkspaceInvitations"
    -- WHERE "Status" = 3 AND "ExpiresAt" < NOW() - INTERVAL '30 days';
    
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;
```

### 3.3 Script de Atualização de Navigation State Metrics

#### IDE.Infrastructure/Scripts/UpdateNavigationMetrics.sql
```sql
-- Script para atualizar métricas de Navigation State
CREATE OR REPLACE FUNCTION update_navigation_metrics()
RETURNS INTEGER AS $$
DECLARE
    updated_count INTEGER := 0;
BEGIN
    -- Atualizar tamanho dos estados de navegação
    UPDATE "WorkspaceNavigationStates"
    SET "Size" = LENGTH("StateJson"::TEXT),
        "UpdatedAt" = NOW()
    WHERE "Size" = 0 OR "Size" IS NULL;
    
    GET DIAGNOSTICS updated_count = ROW_COUNT;
    
    -- Limpar estados não acessados há muito tempo
    DELETE FROM "WorkspaceNavigationStates"
    WHERE "LastAccessedAt" < NOW() - INTERVAL '90 days';
    
    RETURN updated_count;
END;
$$ LANGUAGE plpgsql;
```

## 4. Scripts de Inicialização para Desenvolvimento

### 4.1 Seed Data para Desenvolvimento

#### IDE.Infrastructure/Scripts/DevSeedData.sql
```sql
-- Script para popular dados de desenvolvimento
-- APENAS PARA AMBIENTE DE DESENVOLVIMENTO

-- Criar usuário de teste (se não existir)
INSERT INTO "AspNetUsers" ("Id", "UserName", "Email", "EmailConfirmed", "Plan", "CreatedAt")
SELECT 
    '00000000-0000-0000-0000-000000000001'::uuid,
    'dev@test.com',
    'dev@test.com',
    true,
    'Pro',
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM "AspNetUsers" WHERE "Email" = 'dev@test.com'
);

-- Criar workspace de exemplo
INSERT INTO "Workspaces" ("Id", "Name", "Description", "SemanticVersion", "IsArchived", "CreatedAt", "UpdatedAt", "OwnerId")
SELECT 
    '11111111-0000-0000-0000-000000000001'::uuid,
    'Workspace Demo',
    'Workspace para demonstração e desenvolvimento',
    '1.0.0',
    false,
    NOW(),
    NOW(),
    '00000000-0000-0000-0000-000000000001'::uuid
WHERE NOT EXISTS (
    SELECT 1 FROM "Workspaces" WHERE "Name" = 'Workspace Demo'
);

-- Criar fase padrão
INSERT INTO "WorkspacePhases" ("Id", "Name", "Description", "Color", "Order", "IsDefault", "CreatedAt", "WorkspaceId")
SELECT 
    '22222222-0000-0000-0000-000000000001'::uuid,
    'Development',
    'Fase de desenvolvimento',
    '#52c41a',
    1,
    true,
    NOW(),
    '11111111-0000-0000-0000-000000000001'::uuid
WHERE NOT EXISTS (
    SELECT 1 FROM "WorkspacePhases" WHERE "Name" = 'Development'
);

-- Atualizar workspace com fase atual
UPDATE "Workspaces" 
SET "CurrentPhaseId" = '22222222-0000-0000-0000-000000000001'::uuid
WHERE "Id" = '11111111-0000-0000-0000-000000000001'::uuid;

-- Criar alguns items de exemplo
INSERT INTO "ModuleItems" ("Id", "Name", "Content", "Module", "Type", "Size", "CreatedAt", "UpdatedAt", "WorkspaceId")
VALUES 
(
    '33333333-0000-0000-0000-000000000001'::uuid,
    'README.md',
    '# Projeto Demo\n\nEste é um projeto de demonstração.',
    'Documents',
    'markdown',
    49,
    NOW(),
    NOW(),
    '11111111-0000-0000-0000-000000000001'::uuid
),
(
    '33333333-0000-0000-0000-000000000002'::uuid,
    'UserService.ts',
    'export class UserService {\n  // Implementation\n}',
    'API',
    'typescript',
    48,
    NOW(),
    NOW(),
    '11111111-0000-0000-0000-000000000001'::uuid
);

-- Criar permissão para o usuário de teste
INSERT INTO "WorkspacePermissions" ("Id", "Level", "CreatedAt", "WorkspaceId", "UserId", "IsActive")
SELECT 
    '44444444-0000-0000-0000-000000000001'::uuid,
    0, -- Owner
    NOW(),
    '11111111-0000-0000-0000-000000000001'::uuid,
    '00000000-0000-0000-0000-000000000001'::uuid,
    true
WHERE NOT EXISTS (
    SELECT 1 FROM "WorkspacePermissions" 
    WHERE "WorkspaceId" = '11111111-0000-0000-0000-000000000001'::uuid
    AND "UserId" = '00000000-0000-0000-0000-000000000001'::uuid
);
```

## 5. Configuração de Jobs de Manutenção

### 5.1 Background Service para Limpeza

#### IDE.Infrastructure/Services/MaintenanceBackgroundService.cs
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IDE.Infrastructure.Services
{
    public class MaintenanceBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MaintenanceBackgroundService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(6); // Executa a cada 6 horas

        public MaintenanceBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MaintenanceBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunMaintenanceTasks();
                    await Task.Delay(_period, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro durante execução das tarefas de manutenção");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Retry em 30 min
                }
            }
        }

        private async Task RunMaintenanceTasks()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            _logger.LogInformation("Iniciando tarefas de manutenção");

            // Limpeza de activity logs
            var deletedLogs = await context.Database.ExecuteSqlRawAsync("SELECT cleanup_activity_logs()");
            _logger.LogInformation("Activity logs limpos: {Count}", deletedLogs);

            // Limpeza de convites expirados
            var expiredInvitations = await context.Database.ExecuteSqlRawAsync("SELECT cleanup_expired_invitations()");
            _logger.LogInformation("Convites expirados processados: {Count}", expiredInvitations);

            // Atualização de métricas de navegação
            var updatedStates = await context.Database.ExecuteSqlRawAsync("SELECT update_navigation_metrics()");
            _logger.LogInformation("Estados de navegação atualizados: {Count}", updatedStates);

            _logger.LogInformation("Tarefas de manutenção concluídas");
        }
    }
}
```

## 6. Validação da Migration

### 6.1 Script de Validação

#### IDE.Infrastructure/Scripts/ValidateMigration.sql
```sql
-- Script para validar se a migration foi aplicada corretamente

-- Verificar se todas as tabelas foram criadas
SELECT 
    table_name,
    CASE 
        WHEN table_name = 'Workspaces' THEN '✓'
        WHEN table_name = 'ModuleItems' THEN '✓'
        WHEN table_name = 'WorkspacePhases' THEN '✓'
        WHEN table_name = 'WorkspacePermissions' THEN '✓'
        WHEN table_name = 'WorkspaceInvitations' THEN '✓'
        WHEN table_name = 'ActivityLogs' THEN '✓'
        WHEN table_name = 'ModuleItemTags' THEN '✓'
        WHEN table_name = 'WorkspaceTags' THEN '✓'
        WHEN table_name = 'WorkspaceVersions' THEN '✓'
        WHEN table_name = 'ItemVersions' THEN '✓'
        WHEN table_name = 'WorkspaceNavigationStates' THEN '✓'
        WHEN table_name = 'SystemParameters' THEN '✓'
        ELSE '✗'
    END as status
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name IN (
    'Workspaces', 'ModuleItems', 'WorkspacePhases', 
    'WorkspacePermissions', 'WorkspaceInvitations', 'ActivityLogs',
    'ModuleItemTags', 'WorkspaceTags', 'WorkspaceVersions', 
    'ItemVersions', 'WorkspaceNavigationStates', 'SystemParameters'
);

-- Verificar se os SystemParameters foram inseridos
SELECT 
    category,
    COUNT(*) as parameter_count
FROM "SystemParameters"
GROUP BY category
ORDER BY category;

-- Verificar índices principais
SELECT 
    indexname,
    tablename
FROM pg_indexes 
WHERE tablename IN (
    'Workspaces', 'ModuleItems', 'WorkspacePermissions', 'ActivityLogs'
)
ORDER BY tablename, indexname;
```

## 7. Características Implementadas

✅ **Migration completa** da Fase 2 com todas as tabelas  
✅ **SystemParameters seed** com configurações por categoria  
✅ **Scripts de manutenção** para limpeza automática  
✅ **Background service** para tarefas de manutenção  
✅ **Dados de desenvolvimento** para testes  
✅ **Validação de migration** com scripts SQL  
✅ **Funções PostgreSQL** para operações otimizadas  
✅ **Configuração de jobs** para limpeza automática  

## 8. Próximos Passos

**Parte 6**: DTOs Básicos
- WorkspaceDto, ModuleItemDto
- AutoMapper profiles
- Response objects
- Pagination DTOs

**Validação desta Parte**:
- [ ] Migration executa sem erros
- [ ] SystemParameters são inseridos corretamente
- [ ] Scripts de manutenção funcionam
- [ ] Background service inicia corretamente
- [ ] Dados de desenvolvimento são criados

## 9. Comandos de Execução

### Para aplicar esta parte:

```bash
# 1. Criar e aplicar migration
dotnet ef migrations add "Fase2_WorkspaceCore" --context ApplicationDbContext --startup-project ../IDE.API
dotnet ef database update --context ApplicationDbContext --startup-project ../IDE.API

# 2. Executar seed de desenvolvimento (opcional)
dotnet ef database update --context ApplicationDbContext --startup-project ../IDE.API
# Executar DevSeedData.sql manualmente no banco

# 3. Verificar migration
# Executar ValidateMigration.sql no banco
```

## 10. Notas Importantes

⚠️ **Migration será grande** - pode demorar alguns minutos  
⚠️ **SystemParameters** são críticos - não alterar sem planejamento  
⚠️ **Scripts de limpeza** devem ser testados em ambiente seguro  
⚠️ **Background service** deve ser configurado no Program.cs  
⚠️ **DevSeedData** apenas para desenvolvimento  
⚠️ **Backup do banco** antes de aplicar em produção  

Esta parte estabelece a **base de dados completa** da Fase 2. A próxima parte criará os DTOs para a comunicação entre as camadas.