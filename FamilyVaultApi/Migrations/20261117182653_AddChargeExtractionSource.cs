using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeExtractionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "extraction_source",
                table: "charges",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                // Charges anteriores saíram todas do arquivo (o valor informado à mão é do mesmo dia e não foi para produção)
                defaultValue: "Document")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "extraction_source",
                table: "charges");
        }
    }
}
