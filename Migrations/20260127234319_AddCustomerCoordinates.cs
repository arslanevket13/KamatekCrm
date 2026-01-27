using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Customers",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Customers",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Customers");
        }
    }
}
