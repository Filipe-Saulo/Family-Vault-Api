using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gmail_scan_id",
                table: "inbound_documents",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gmail_connections",
                columns: table => new
                {
                    gmail_connection_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    google_email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    refresh_token_encrypted = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    connected_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    initial_scan_completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    synced_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_scan_requested_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_connections", x => x.gmail_connection_id);
                    table.ForeignKey(
                        name: "fk_gmail_connections_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gmail_ignored_senders",
                columns: table => new
                {
                    gmail_ignored_sender_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sender = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_ignored_senders", x => x.gmail_ignored_sender_id);
                    table.ForeignKey(
                        name: "fk_gmail_ignored_senders_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gmail_oauth_states",
                columns: table => new
                {
                    gmail_oauth_state_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    state_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    return_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_oauth_states", x => x.gmail_oauth_state_id);
                    table.ForeignKey(
                        name: "fk_gmail_oauth_states_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gmail_scans",
                columns: table => new
                {
                    gmail_scan_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    window_start = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    window_end = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    found = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    processed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_scans", x => x.gmail_scan_id);
                    table.ForeignKey(
                        name: "fk_gmail_scans_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_documents_gmail_scan_id",
                table: "inbound_documents",
                column: "gmail_scan_id");

            migrationBuilder.CreateIndex(
                name: "idx_gmail_connections_user",
                table: "gmail_connections",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_gmail_ignored_senders_user_sender",
                table: "gmail_ignored_senders",
                columns: new[] { "user_id", "sender" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_gmail_oauth_states_expires",
                table: "gmail_oauth_states",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_gmail_oauth_states_hash",
                table: "gmail_oauth_states",
                column: "state_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gmail_oauth_states_user_id",
                table: "gmail_oauth_states",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_gmail_scans_status_created",
                table: "gmail_scans",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_gmail_scans_user_status",
                table: "gmail_scans",
                columns: new[] { "user_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_inbound_documents_gmail_scan",
                table: "inbound_documents",
                column: "gmail_scan_id",
                principalTable: "gmail_scans",
                principalColumn: "gmail_scan_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_inbound_documents_gmail_scan",
                table: "inbound_documents");

            migrationBuilder.DropTable(
                name: "gmail_connections");

            migrationBuilder.DropTable(
                name: "gmail_ignored_senders");

            migrationBuilder.DropTable(
                name: "gmail_oauth_states");

            migrationBuilder.DropTable(
                name: "gmail_scans");

            migrationBuilder.DropIndex(
                name: "IX_inbound_documents_gmail_scan_id",
                table: "inbound_documents");

            migrationBuilder.DropColumn(
                name: "gmail_scan_id",
                table: "inbound_documents");
        }
    }
}
