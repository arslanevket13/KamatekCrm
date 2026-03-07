using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.API.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sadece ServiceProjects tablosuna yeni alanlar ekle
            migrationBuilder.AddColumn<string>(
                name: "QuoteNumber",
                table: "ServiceProjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuoteStatus",
                table: "ServiceProjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                table: "ServiceProjects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentDate",
                table: "ServiceProjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "ServiceProjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "ServiceProjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedDate",
                table: "ServiceProjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ServiceProjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KdvRate",
                table: "ServiceProjects",
                type: "numeric",
                nullable: false,
                defaultValue: 20m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ServiceProjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "ServiceProjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisionsJson",
                table: "ServiceProjects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "QuoteNumber", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "QuoteStatus", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "RevisionNumber", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "SentDate", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "ValidUntil", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "ApprovedDate", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "RejectedDate", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "KdvRate", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "Notes", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "PaymentTerms", table: "ServiceProjects");
            migrationBuilder.DropColumn(name: "RevisionsJson", table: "ServiceProjects");
        }
    }
}
