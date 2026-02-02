using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeToProfessionalERP_Phase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "PurchaseOrders",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "PurchaseOrders",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "Inventories",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "Inventories");
        }
    }
}
