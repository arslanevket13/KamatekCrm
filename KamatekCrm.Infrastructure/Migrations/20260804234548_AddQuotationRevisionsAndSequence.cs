using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationRevisionsAndSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentQuotationId",
                table: "WorkOrderQuotations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "WorkOrderQuotations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "QuotationItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "QuotationItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InstallationMaterials",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentQuotationId",
                table: "WorkOrderQuotations");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                table: "WorkOrderQuotations");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "QuotationItems");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "QuotationItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "InstallationMaterials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
