using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KamatekCrm.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260801205000_ReconcileQuoteAndUserSchema")]
public partial class ReconcileQuoteAndUserSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Currency",
            table: "Quotes",
            type: "text",
            nullable: false,
            defaultValue: "TRY");

        migrationBuilder.AddColumn<string>(
            name: "QuoteTitle",
            table: "Quotes",
            type: "text",
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<decimal>(
            name: "PurchasePrice",
            table: "QuoteLines",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AlterColumn<string>(
            name: "ExpertiseAreas",
            table: "Users",
            type: "character varying(250)",
            maxLength: 250,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Currency", table: "Quotes");
        migrationBuilder.DropColumn(name: "QuoteTitle", table: "Quotes");
        migrationBuilder.DropColumn(name: "PurchasePrice", table: "QuoteLines");

        migrationBuilder.AlterColumn<string>(
            name: "ExpertiseAreas",
            table: "Users",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(250)",
            oldMaxLength: 250,
            oldNullable: true);
    }
}
