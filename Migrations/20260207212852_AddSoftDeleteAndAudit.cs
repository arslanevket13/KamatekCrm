using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Warehouses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Warehouses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Warehouses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Warehouses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Warehouses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Warehouses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Warehouses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Suppliers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Suppliers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Suppliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Suppliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Suppliers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Suppliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Suppliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedDate",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedTechnicianId",
                table: "ServiceJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ServiceJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ServiceJobs",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ServiceJobHistories",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "ServiceJobHistories",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ServiceJobHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ServiceJobHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ServiceJobHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "ServiceJobHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "ServiceJobHistories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "CustomerAssets",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "CustomerAssets",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedBy", "CreatedDate", "DeletedAt", "DeletedBy", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, false, null, null });

            migrationBuilder.UpdateData(
                table: "Warehouses",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedBy", "CreatedDate", "DeletedAt", "DeletedBy", "IsDeleted", "ModifiedBy", "ModifiedDate" },
                values: new object[] { "", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null, false, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "ServiceJobHistories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Customers");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CompletedDate",
                table: "ServiceJobs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SerialNumber",
                table: "CustomerAssets",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "CustomerAssets",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
