using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceJobId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveryNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CustomerSignature = table.Column<string>(type: "text", nullable: true),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobDeliveries_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobDeliveries_ServiceJobId",
                table: "JobDeliveries",
                column: "ServiceJobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobDeliveries");
        }
    }
}
