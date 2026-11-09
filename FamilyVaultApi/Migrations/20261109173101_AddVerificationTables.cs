using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_verification_codes",
                columns: table => new
                {
                    destination = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    purpose = table.Column<int>(type: "int", nullable: false),
                    code_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    failed_attempts = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_verification_codes", x => new { x.destination, x.purpose });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tb_verification_send_limits",
                columns: table => new
                {
                    destination = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    day_window_started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    day_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_verification_send_limits", x => x.destination);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_verification_codes_expires",
                table: "tb_verification_codes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_verification_send_limits_window",
                table: "tb_verification_send_limits",
                column: "day_window_started_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_verification_codes");

            migrationBuilder.DropTable(
                name: "tb_verification_send_limits");
        }
    }
}
