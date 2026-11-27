using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIsDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "tb_user",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "tb_user");
        }
    }
}
