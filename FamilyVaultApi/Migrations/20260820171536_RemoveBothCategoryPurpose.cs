using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBothCategoryPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reassign any category still pointing at purpose_id=3 ("both") - including rows created
            // ad hoc outside the seed - to Expense, so the FK-blocked delete below can proceed.
            migrationBuilder.Sql("UPDATE `categories` SET `category_purpose_id` = 1 WHERE `category_purpose_id` = 3;");

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1966));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1967));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1968));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 4,
                columns: new[] { "category_purpose_id", "created_at", "description" },
                values: new object[] { 1, new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1969), "Aporte em Investimentos" });

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 5,
                columns: new[] { "category_purpose_id", "created_at", "description" },
                values: new object[] { 2, new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1970), "Rendimento em Investimentos" });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "category_id", "category_purpose_id", "created_at", "description" },
                values: new object[] { 6, 1, new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1971), "Lazer" });

            migrationBuilder.DeleteData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1889));

            migrationBuilder.UpdateData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1891));

            migrationBuilder.UpdateData(
                table: "transaction_types",
                keyColumn: "transaction_type_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1942));

            migrationBuilder.UpdateData(
                table: "transaction_types",
                keyColumn: "transaction_type_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1944));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 6);

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5072));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5073));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5075));

            migrationBuilder.UpdateData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5002));

            migrationBuilder.UpdateData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5005));

            migrationBuilder.InsertData(
                table: "category_purposes",
                columns: new[] { "category_purpose_id", "code", "created_at", "description", "is_active", "name" },
                values: new object[] { 3, "both", new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5006), "Para despesas e receitas", true, "Ambas" });

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 4,
                columns: new[] { "category_purpose_id", "created_at", "description" },
                values: new object[] { 3, new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5076), "Investimentos" });

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 5,
                columns: new[] { "category_purpose_id", "created_at", "description" },
                values: new object[] { 3, new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5077), "Lazer" });

            migrationBuilder.UpdateData(
                table: "transaction_types",
                keyColumn: "transaction_type_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5025));

            migrationBuilder.UpdateData(
                table: "transaction_types",
                keyColumn: "transaction_type_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 7, 30, 22, 13, 39, 890, DateTimeKind.Utc).AddTicks(5027));
        }
    }
}
