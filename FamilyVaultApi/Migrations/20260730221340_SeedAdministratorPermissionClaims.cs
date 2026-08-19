using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FamilyVaultApi.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdministratorPermissionClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "tb_role_claims",
                columns: new[] { "Id", "ClaimType", "ClaimValue", "RoleId" },
                values: new object[,]
                {
                    { 1, "permission", "ManageCategories", "5d92e12f-f00c-4899-ac98-89ea76712171" },
                    { 2, "permission", "ManageTransactionTypes", "5d92e12f-f00c-4899-ac98-89ea76712171" },
                    { 3, "permission", "ManageTransactions", "5d92e12f-f00c-4899-ac98-89ea76712171" },
                    { 4, "permission", "ManageUsers", "5d92e12f-f00c-4899-ac98-89ea76712171" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "tb_role_claims",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "tb_role_claims",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "tb_role_claims",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "tb_role_claims",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
