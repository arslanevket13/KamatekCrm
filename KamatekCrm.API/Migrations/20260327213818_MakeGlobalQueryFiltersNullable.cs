using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.API.Migrations
{
    /// <inheritdoc />
    public partial class MakeGlobalQueryFiltersNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseId",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLine_PurchaseInvoices_PurchaseInvoiceId",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceJobId",
                table: "ServiceJobItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseInvoiceId",
                table: "PurchaseInvoiceLine",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "WarehouseId",
                table: "Inventories",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseId",
                table: "Inventories",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLine_PurchaseInvoices_PurchaseInvoiceId",
                table: "PurchaseInvoiceLine",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems",
                column: "ServiceJobId",
                principalTable: "ServiceJobs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseId",
                table: "Inventories");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLine_PurchaseInvoices_PurchaseInvoiceId",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceJobId",
                table: "ServiceJobItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PurchaseInvoiceId",
                table: "PurchaseInvoiceLine",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "WarehouseId",
                table: "Inventories",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Warehouses_WarehouseId",
                table: "Inventories",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLine_PurchaseInvoices_PurchaseInvoiceId",
                table: "PurchaseInvoiceLine",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems",
                column: "ServiceJobId",
                principalTable: "ServiceJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
