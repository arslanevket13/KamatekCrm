using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.API.Migrations
{
    /// <inheritdoc />
    public partial class FixFinalMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "ServiceJobs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "ServiceJobs",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "ServiceJobs");
        }
    }
}
