using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class ErpModulesPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosTransactionLine_Products_ProductId",
                table: "PosTransactionLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PosTransactions_Customers_CustomerId",
                table: "PosTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLine_Products_ProductId",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "PosTransactions");

            migrationBuilder.AddColumn<decimal>(
                name: "GrandTotal",
                table: "PurchaseInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PurchaseInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrRawText",
                table: "PurchaseInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "PurchaseInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "PurchaseInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "PurchaseInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "PurchaseInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotal",
                table: "PurchaseInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "PurchaseInvoiceLine",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotal",
                table: "PurchaseInvoiceLine",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "NewAverageCost",
                table: "PurchaseInvoiceLine",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OldAverageCost",
                table: "PurchaseInvoiceLine",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "PurchaseInvoiceLine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "PurchaseInvoiceLine",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "PurchaseInvoiceLine",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "AverageCost",
                table: "Products",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "CardAmount",
                table: "PosTransactions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CashAmount",
                table: "PosTransactions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CashierUserId",
                table: "PosTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                table: "PosTransactions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrandTotal",
                table: "PosTransactions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PosTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PosTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "PosTransactions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatTotal",
                table: "PosTransactions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "PosTransactionLine",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotal",
                table: "PosTransactionLine",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PosTransactionLine",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "PosTransactionLine",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "PosTransactionLine",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetTotal",
                table: "PosTransactionLine",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "PosTransactionLine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "PosTransactionLine",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "PosTransactionLine",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 20, 21, 43, 34, 96, DateTimeKind.Utc).AddTicks(1009));

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 20, 21, 43, 34, 96, DateTimeKind.Utc).AddTicks(1928));

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_PosTransactions_CashierUserId",
                table: "PosTransactions",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosTransactions_TransactionNumber",
                table: "PosTransactions",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PosTransactionLine_Products_ProductId",
                table: "PosTransactionLine",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PosTransactions_Customers_CustomerId",
                table: "PosTransactions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PosTransactions_Users_CashierUserId",
                table: "PosTransactions",
                column: "CashierUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLine_Products_ProductId",
                table: "PurchaseInvoiceLine",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosTransactionLine_Products_ProductId",
                table: "PosTransactionLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PosTransactions_Customers_CustomerId",
                table: "PosTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PosTransactions_Users_CashierUserId",
                table: "PosTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLine_Products_ProductId",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_PosTransactions_CashierUserId",
                table: "PosTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PosTransactions_TransactionNumber",
                table: "PosTransactions");

            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "GrandTotal",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "OcrRawText",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "VatTotal",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "NewAverageCost",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "OldAverageCost",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "CardAmount",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "CashAmount",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "CashierUserId",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "GrandTotal",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "VatTotal",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "NetTotal",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "PosTransactionLine");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "PurchaseInvoiceLine",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotal",
                table: "PurchaseInvoiceLine",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "AverageCost",
                table: "Products",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "PosTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "PosTransactionLine",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotal",
                table: "PosTransactionLine",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 20, 20, 48, 36, 578, DateTimeKind.Utc).AddTicks(9385));

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 2, 20, 20, 48, 36, 579, DateTimeKind.Utc).AddTicks(118));

            migrationBuilder.AddForeignKey(
                name: "FK_PosTransactionLine_Products_ProductId",
                table: "PosTransactionLine",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PosTransactions_Customers_CustomerId",
                table: "PosTransactions",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLine_Products_ProductId",
                table: "PurchaseInvoiceLine",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
