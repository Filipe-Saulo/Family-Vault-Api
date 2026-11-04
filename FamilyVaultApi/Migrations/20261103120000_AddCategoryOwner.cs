using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_id",
                table: "categories",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_categories_user",
                table: "categories",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_categories_user",
                table: "categories",
                column: "user_id",
                principalTable: "tb_user",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);

            // Usuários anteriores ao lockout passam a poder ser bloqueados (AllowedForNewUsers só vale para novos).
            migrationBuilder.Sql("UPDATE `tb_user` SET `LockoutEnabled` = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O UPDATE LockoutEnabled = 1 do Up não é revertido de propósito: o estado anterior de cada usuário não é conhecido.
            migrationBuilder.DropForeignKey(
                name: "fk_categories_user",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "idx_categories_user",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "categories");
        }
    }
}
