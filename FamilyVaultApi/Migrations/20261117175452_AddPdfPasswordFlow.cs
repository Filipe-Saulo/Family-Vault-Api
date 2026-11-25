using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfPasswordFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "password_attempts",
                table: "inbound_documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_required_at",
                table: "inbound_documents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pdf_sender_passwords",
                columns: table => new
                {
                    pdf_sender_password_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sender = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_encrypted = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_sender_passwords", x => x.pdf_sender_password_id);
                    table.ForeignKey(
                        name: "fk_pdf_sender_passwords_user",
                        column: x => x.user_id,
                        principalTable: "tb_user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_pdf_sender_passwords_user_sender",
                table: "pdf_sender_passwords",
                columns: new[] { "user_id", "sender" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdf_sender_passwords");

            migrationBuilder.DropColumn(
                name: "password_attempts",
                table: "inbound_documents");

            migrationBuilder.DropColumn(
                name: "password_required_at",
                table: "inbound_documents");
        }
    }
}
