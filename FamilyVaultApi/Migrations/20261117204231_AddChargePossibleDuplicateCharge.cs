using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class AddChargePossibleDuplicateCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "possible_duplicate_charge_id",
                table: "charges",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_charges_possible_duplicate_charge",
                table: "charges",
                column: "possible_duplicate_charge_id");

            migrationBuilder.AddForeignKey(
                name: "fk_charges_possible_duplicate_charge",
                table: "charges",
                column: "possible_duplicate_charge_id",
                principalTable: "charges",
                principalColumn: "charge_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_charges_possible_duplicate_charge",
                table: "charges");

            migrationBuilder.DropIndex(
                name: "idx_charges_possible_duplicate_charge",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "possible_duplicate_charge_id",
                table: "charges");
        }
    }
}
