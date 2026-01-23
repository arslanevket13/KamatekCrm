using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairTrackingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Accessories",
                table: "ServiceJobs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceBrand",
                table: "ServiceJobs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceModel",
                table: "ServiceJobs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPathsJson",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalCondition",
                table: "ServiceJobs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepairStatus",
                table: "ServiceJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "ServiceJobs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceJobHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StatusChange = table.Column<int>(type: "INTEGER", nullable: true),
                    TechnicianNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceJobHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceJobHistories_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobHistories_ServiceJobId",
                table: "ServiceJobHistories",
                column: "ServiceJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "Accessories",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "DeviceBrand",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "DeviceModel",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "PhotoPathsJson",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "PhysicalCondition",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "RepairStatus",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "ServiceJobs");
        }
    }
}
