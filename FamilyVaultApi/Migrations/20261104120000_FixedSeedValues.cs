using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class FixedSeedValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 3,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 4,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 5,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 6,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "category_purposes",
                keyColumn: "category_purpose_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "transaction_types",
                keyColumn: "transaction_type_id",
                keyValue: 1,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "transaction_types",
                keyColumn: "transaction_type_id",
                keyValue: 2,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "tb_roles",
                keyColumn: "Id",
                keyValue: "5d92e12f-f00c-4899-ac98-89ea76712171",
                column: "ConcurrencyStamp",
                value: "8f3c1a52-6b7e-4d19-9a40-2c5e7d1b3f68");

            migrationBuilder.UpdateData(
                table: "tb_roles",
                keyColumn: "Id",
                keyValue: "bad15740-437c-4721-839c-a9af6fbd3961",
                column: "ConcurrencyStamp",
                value: "e2b94d07-1c58-4a63-b7f1-5d8a0c3e9246");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1969));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 5,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1970));

            migrationBuilder.UpdateData(
                table: "categories",
                keyColumn: "category_id",
                keyValue: 6,
                column: "created_at",
                value: new DateTime(2026, 8, 20, 17, 15, 34, 331, DateTimeKind.Utc).AddTicks(1971));

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

            migrationBuilder.UpdateData(
                table: "tb_roles",
                keyColumn: "Id",
                keyValue: "5d92e12f-f00c-4899-ac98-89ea76712171",
                column: "ConcurrencyStamp",
                value: null);

            migrationBuilder.UpdateData(
                table: "tb_roles",
                keyColumn: "Id",
                keyValue: "bad15740-437c-4721-839c-a9af6fbd3961",
                column: "ConcurrencyStamp",
                value: null);
        }
    }
}
