using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vanq.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenUserCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_CreatedAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_CreatedAt",
                table: "RefreshTokens");
        }
    }
}
