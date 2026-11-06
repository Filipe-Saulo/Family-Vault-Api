using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_refresh_tokens",
                columns: table => new
                {
                    refresh_token_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    family_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    client = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_refresh_tokens", x => x.refresh_token_id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_family",
                table: "tb_refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_user",
                table: "tb_refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_refresh_tokens_hash",
                table: "tb_refresh_tokens",
                column: "token_hash",
                unique: true);

            // Os refresh antigos ficavam no Identity e não valem mais: quem estava logado entra de novo
            migrationBuilder.Sql(
                "DELETE FROM tb_user_tokens WHERE Name IN ('RefreshToken', 'RefreshToken-web', 'RefreshToken-app');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_refresh_tokens");
        }
    }
}
