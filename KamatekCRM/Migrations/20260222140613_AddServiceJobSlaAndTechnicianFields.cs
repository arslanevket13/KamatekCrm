using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceJobSlaAndTechnicianFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentGpsLocation",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpertiseAreas",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnDuty",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTechnician",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationUpdate",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceArea",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalJobsCompleted",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlate",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualDuration",
                table: "ServiceJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerSignature",
                table: "ServiceJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDuration",
                table: "ServiceJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GpsLocation",
                table: "ServiceJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomerApproved",
                table: "ServiceJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOffSite",
                table: "ServiceJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaDeadline",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ServiceJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianNotes",
                table: "ServiceJobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 22, 14, 6, 13, 16, DateTimeKind.Utc).AddTicks(5553));

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 22, 14, 6, 13, 16, DateTimeKind.Utc).AddTicks(6387));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentGpsLocation",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExpertiseAreas",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsOnDuty",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsTechnician",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLocationUpdate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ServiceArea",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotalJobsCompleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VehiclePlate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActualDuration",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "CustomerSignature",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "GpsLocation",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "IsCustomerApproved",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "IsOffSite",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "SlaDeadline",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "TechnicianNotes",
                table: "ServiceJobs");

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 22, 13, 44, 30, 31, DateTimeKind.Utc).AddTicks(7095));

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 22, 13, 44, 30, 31, DateTimeKind.Utc).AddTicks(7790));
        }
    }
}
