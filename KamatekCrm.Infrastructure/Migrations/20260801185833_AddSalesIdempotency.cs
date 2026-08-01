using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Satış tekrarlarını engelleyen istemci işlem anahtarını ekler.
    /// Mevcut kayıtlar null bırakıldığı için canlı veriye zarar vermez.
    /// </summary>
    public partial class AddSalesIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "SalesOrders",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_IdempotencyKey",
                table: "SalesOrders",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_IdempotencyKey",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "SalesOrders");
        }
    }
}
