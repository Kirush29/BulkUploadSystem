using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulkImportPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeUniquenessPerClientAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_VehicleNumber",
                table: "Vehicles");

            migrationBuilder.AddColumn<int>(
                name: "ClientAccountId",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientAccountId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ClientAccountId_VehicleNumber",
                table: "Vehicles",
                columns: new[] { "ClientAccountId", "VehicleNumber" },
                unique: true,
                filter: "[ClientAccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ClientAccountId",
                table: "Customers",
                column: "ClientAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_ClientAccounts_ClientAccountId",
                table: "Customers",
                column: "ClientAccountId",
                principalTable: "ClientAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_ClientAccounts_ClientAccountId",
                table: "Vehicles",
                column: "ClientAccountId",
                principalTable: "ClientAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_ClientAccounts_ClientAccountId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_ClientAccounts_ClientAccountId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_ClientAccountId_VehicleNumber",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ClientAccountId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ClientAccountId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ClientAccountId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleNumber",
                table: "Vehicles",
                column: "VehicleNumber",
                unique: true);
        }
    }
}
