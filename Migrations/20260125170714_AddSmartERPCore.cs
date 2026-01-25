using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartERPCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PipelineStage",
                table: "ServiceProjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AssignedUserId",
                table: "ServiceJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FrequencyInMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PricePerVisit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    JobDescriptionTemplate = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceContracts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_AssignedUserId",
                table: "ServiceJobs",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceContracts_CustomerId",
                table: "MaintenanceContracts",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobs_Users_AssignedUserId",
                table: "ServiceJobs",
                column: "AssignedUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobs_Users_AssignedUserId",
                table: "ServiceJobs");

            migrationBuilder.DropTable(
                name: "MaintenanceContracts");

            migrationBuilder.DropIndex(
                name: "IX_ServiceJobs_AssignedUserId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "PipelineStage",
                table: "ServiceProjects");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "ServiceJobs");
        }
    }
}
