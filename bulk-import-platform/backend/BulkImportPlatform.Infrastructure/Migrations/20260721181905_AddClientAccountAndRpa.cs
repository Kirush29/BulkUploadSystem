using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BulkImportPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientAccountAndRpa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RpaEntityReference",
                table: "ImportRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RpaMessage",
                table: "ImportRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RpaStatus",
                table: "ImportRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClientAccountId",
                table: "ImportJobs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RpaCurrentLocation",
                table: "ImportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RpaOwningLocation",
                table: "ImportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IrentosoftUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RpaAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportRecordId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    FailureStage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScreenshotPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RpaAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RpaAttempts_ImportRecords_ImportRecordId",
                        column: x => x.ImportRecordId,
                        principalTable: "ImportRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_ClientAccountId",
                table: "ImportJobs",
                column: "ClientAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RpaAttempts_ImportRecordId",
                table: "RpaAttempts",
                column: "ImportRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportJobs_ClientAccounts_ClientAccountId",
                table: "ImportJobs",
                column: "ClientAccountId",
                principalTable: "ClientAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportJobs_ClientAccounts_ClientAccountId",
                table: "ImportJobs");

            migrationBuilder.DropTable(
                name: "ClientAccounts");

            migrationBuilder.DropTable(
                name: "RpaAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_ClientAccountId",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "RpaEntityReference",
                table: "ImportRecords");

            migrationBuilder.DropColumn(
                name: "RpaMessage",
                table: "ImportRecords");

            migrationBuilder.DropColumn(
                name: "RpaStatus",
                table: "ImportRecords");

            migrationBuilder.DropColumn(
                name: "ClientAccountId",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "RpaCurrentLocation",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "RpaOwningLocation",
                table: "ImportJobs");
        }
    }
}
