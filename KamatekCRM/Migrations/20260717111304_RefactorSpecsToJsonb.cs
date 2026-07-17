using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSpecsToJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobHistories_ServiceJobs_ServiceJobId",
                table: "ServiceJobHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskPhotos_ServiceJobs_TaskId",
                table: "TaskPhotos");

            migrationBuilder.RenameColumn(
                name: "TechSpecsJson",
                table: "Products",
                newName: "Specifications");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Warehouses",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Transactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "TechnicianLocations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<int>(
                name: "TaskId",
                table: "TaskPhotos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "TaskPhotos",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Suppliers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Suppliers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Suppliers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "StockTransactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "StockReservations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ServiceProjects",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "JobDetails",
                table: "ServiceJobs",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ServiceJobs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceJobId",
                table: "ServiceJobItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ServiceJobItems",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceJobId",
                table: "ServiceJobHistories",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ServiceJobHistories",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SalesOrders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SalesOrderPayments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SalesOrderItems",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RoutePoints",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseOrders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseOrderItems",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseInvoices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PurchaseInvoiceLine",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProductSerials",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PosTransactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "PosTransactionLine",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MaintenanceContracts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "InventoryImages",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Inventories",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Customers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CustomerAssets",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CustomerActivities",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Categories",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CashTransactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Brands",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Attachments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ActivityLogs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    ProductCode = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuoteLines_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 11, 13, 3, 826, DateTimeKind.Utc).AddTicks(455));

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 7, 17, 11, 13, 3, 826, DateTimeKind.Utc).AddTicks(1146));

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_ProductId",
                table: "QuoteLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_QuoteId",
                table: "QuoteLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobHistories_ServiceJobs_ServiceJobId",
                table: "ServiceJobHistories",
                column: "ServiceJobId",
                principalTable: "ServiceJobs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems",
                column: "ServiceJobId",
                principalTable: "ServiceJobs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskPhotos_ServiceJobs_TaskId",
                table: "TaskPhotos",
                column: "TaskId",
                principalTable: "ServiceJobs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobHistories_ServiceJobs_ServiceJobId",
                table: "ServiceJobHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskPhotos_ServiceJobs_TaskId",
                table: "TaskPhotos");

            migrationBuilder.DropTable(
                name: "QuoteLines");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "TechnicianLocations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "TaskPhotos");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ServiceProjects");

            migrationBuilder.DropColumn(
                name: "JobDetails",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ServiceJobItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SalesOrderPayments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RoutePoints");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PurchaseInvoiceLine");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProductSerials");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PosTransactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "PosTransactionLine");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MaintenanceContracts");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "InventoryImages");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CustomerAssets");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CustomerActivities");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CashTransactions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ActivityLogs");

            migrationBuilder.RenameColumn(
                name: "Specifications",
                table: "Products",
                newName: "TechSpecsJson");

            migrationBuilder.AlterColumn<int>(
                name: "TaskId",
                table: "TaskPhotos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

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
                name: "ServiceJobId",
                table: "ServiceJobHistories",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 19, 19, 56, 45, 598, DateTimeKind.Utc).AddTicks(4183));

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 19, 19, 56, 45, 598, DateTimeKind.Utc).AddTicks(4937));

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobHistories_ServiceJobs_ServiceJobId",
                table: "ServiceJobHistories",
                column: "ServiceJobId",
                principalTable: "ServiceJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobItems_ServiceJobs_ServiceJobId",
                table: "ServiceJobItems",
                column: "ServiceJobId",
                principalTable: "ServiceJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskPhotos_ServiceJobs_TaskId",
                table: "TaskPhotos",
                column: "TaskId",
                principalTable: "ServiceJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
