using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vanq.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlagsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastUpdatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[,]
                {
                    { new Guid("03a95e32-ee67-d688-0f58-7839b6c5bac5"), "Grava auditoria completa de alterações (SPEC-0006-V2)", "Development", false, "feature-flags-audit-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("05c9c5cc-f8f2-e04a-a5d7-b11a859ad33e"), "Habilita política CORS permissiva para dev/debug (SPEC-0002)", "Production", false, "cors-relaxed", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("09171696-5e3c-0bd6-20fa-987dbec68f68"), "Métricas detalhadas de autenticação (SPEC-0010)", "Staging", false, "metrics-detailed-auth", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("097268dd-0584-14fd-6156-cd7d73ff3fb4"), "Exporta métricas Prometheus (SPEC-0010)", "Development", true, "metrics-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("0ae53468-3990-d249-3e46-42109c7ab1dd"), "Usa Problem Details (RFC 7807) em erros (SPEC-0003)", "Production", false, "problem-details-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null }
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsCritical", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[] { new Guid("1c2f53ab-2f94-aa2c-ede9-7e147d72ac88"), "Habilita o próprio módulo de feature flags", "Production", true, true, "feature-flags-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[,]
                {
                    { new Guid("251daf83-ee7d-7be4-037a-0c90b33e96a6"), "Habilita módulo de parâmetros do sistema (SPEC-0007)", "Production", false, "system-params-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("2ac0334f-5dc5-af51-fb34-fdf3f32b2b05"), "Grava auditoria completa de alterações (SPEC-0006-V2)", "Staging", false, "feature-flags-audit-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("2adace35-c2b8-3182-9a4a-ea1b25cb26a8"), "Métricas detalhadas de autenticação (SPEC-0010)", "Development", true, "metrics-detailed-auth", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("2d1035ca-1b41-e335-320e-55ed6fd0b3a9"), "Ativa rate limiting global (SPEC-0008)", "Staging", true, "rate-limiting-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("2d52b6e9-d532-b756-eb17-5a5b69703181"), "Ativa middleware global de tratamento de erros (SPEC-0005)", "Development", true, "error-middleware-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("45462cd5-0f47-b021-d36a-64bef233f298"), "Habilita módulo de parâmetros do sistema (SPEC-0007)", "Development", true, "system-params-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("5fff5a42-856a-a4bd-45a6-a6f3f28e7a3b"), "Usa Serilog com enriquecimento estruturado (SPEC-0009)", "Staging", true, "structured-logging-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null }
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsCritical", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[] { new Guid("663ab5cb-8993-9f5b-c45d-62ef8b37d126"), "Habilita sistema RBAC (migrado de RbacOptions.FeatureEnabled)", "Staging", true, true, "rbac-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[,]
                {
                    { new Guid("744c57d8-6118-24f5-a4bb-23fd8a8e1832"), "Permite registro de novos usuários (SPEC-0001)", "Production", true, "user-registration-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("745531e5-437d-1eef-155d-952bfc4b39b9"), "Permite registro de novos usuários (SPEC-0001)", "Development", true, "user-registration-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("78d6d418-7b7b-e291-baf8-44dbb8798744"), "Grava auditoria completa de alterações (SPEC-0006-V2)", "Production", false, "feature-flags-audit-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("888d90a9-370c-7848-716e-f6fc2ea27090"), "Ativa middleware global de tratamento de erros (SPEC-0005)", "Staging", true, "error-middleware-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("8e40b372-45d0-0b7b-0374-b3a225a78b81"), "Expõe endpoints de health check (SPEC-0004)", "Staging", true, "health-checks-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("9c8e1dd4-9916-53a8-bd2a-0c4fe987bf64"), "Habilita política CORS permissiva para dev/debug (SPEC-0002)", "Development", true, "cors-relaxed", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("9dab97f4-e16a-5caf-c9dd-2c621428b4b1"), "Usa Serilog com enriquecimento estruturado (SPEC-0009)", "Development", true, "structured-logging-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("a7a2021a-d33f-aa3b-76b4-06ebaed95731"), "Usa Problem Details (RFC 7807) em erros (SPEC-0003)", "Development", true, "problem-details-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("aa4cc1a5-028b-2486-01c3-f74f786acf8f"), "Expõe endpoints de health check (SPEC-0004)", "Development", true, "health-checks-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null }
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsCritical", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[,]
                {
                    { new Guid("b25ee248-5561-04cc-974a-15a197ad1c6b"), "Habilita o próprio módulo de feature flags", "Staging", true, true, "feature-flags-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("b66b3373-38de-e53c-65f2-42f2089ed638"), "Habilita sistema RBAC (migrado de RbacOptions.FeatureEnabled)", "Development", true, true, "rbac-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null }
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[,]
                {
                    { new Guid("b89aaf67-b8de-a805-e72d-d2b93e938790"), "Expõe endpoints de health check (SPEC-0004)", "Production", true, "health-checks-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("c3737442-9abf-d4a7-2cf8-1a99b352dbba"), "Habilita política CORS permissiva para dev/debug (SPEC-0002)", "Staging", false, "cors-relaxed", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("c85ee62e-d73a-7e45-a79e-173f63b2c708"), "Métricas detalhadas de autenticação (SPEC-0010)", "Production", false, "metrics-detailed-auth", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("cf7be7e2-6760-440f-2ebb-1f2206bd88b7"), "Permite registro de novos usuários (SPEC-0001)", "Staging", true, "user-registration-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("ddc1a5b5-9164-6982-8871-4ccbc63dd1e4"), "Ativa rate limiting global (SPEC-0008)", "Production", true, "rate-limiting-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null }
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsCritical", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[] { new Guid("e70c8882-5b6d-99f4-ee31-cf1bf30e0244"), "Habilita sistema RBAC (migrado de RbacOptions.FeatureEnabled)", "Production", true, true, "rbac-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[,]
                {
                    { new Guid("eb63f5ab-f359-779a-e7df-c33ddeaee7c0"), "Usa Serilog com enriquecimento estruturado (SPEC-0009)", "Production", true, "structured-logging-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("f1ce2131-826e-d501-3cf7-e84b4b7f4ec0"), "Exporta métricas Prometheus (SPEC-0010)", "Production", true, "metrics-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("f6d20774-a2ca-0790-7c69-de2b37910be7"), "Ativa rate limiting global (SPEC-0008)", "Development", false, "rate-limiting-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("f8226784-0600-57db-e475-505996722505"), "Usa Problem Details (RFC 7807) em erros (SPEC-0003)", "Staging", true, "problem-details-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("f8d2c3b4-c47a-3f88-eb49-531ee7bc3ee4"), "Exporta métricas Prometheus (SPEC-0010)", "Staging", true, "metrics-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("fcbac6b3-4a43-cff2-a509-1a8b18741db5"), "Ativa middleware global de tratamento de erros (SPEC-0005)", "Production", false, "error-middleware-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null },
                    { new Guid("fcdc580b-7744-a134-7a2d-e514915e2936"), "Habilita módulo de parâmetros do sistema (SPEC-0007)", "Staging", true, "system-params-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null }
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "Description", "Environment", "IsCritical", "IsEnabled", "Key", "LastUpdatedAt", "LastUpdatedBy", "Metadata" },
                values: new object[] { new Guid("fdaab4c5-befd-4a2a-a39f-276556d568bc"), "Habilita o próprio módulo de feature flags", "Development", true, true, "feature-flags-enabled", new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system-seed", null });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Environment",
                table: "FeatureFlags",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Key_Environment",
                table: "FeatureFlags",
                columns: new[] { "Key", "Environment" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureFlags");
        }
    }
}
