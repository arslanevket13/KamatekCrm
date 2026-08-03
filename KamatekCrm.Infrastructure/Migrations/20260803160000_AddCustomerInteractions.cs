using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InteractionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CallerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CallerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NormalizedPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    RequestType = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetailedNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InteractionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUsername = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AssignedToUserId = table.Column<int>(type: "integer", nullable: true),
                    AssignedToUsername = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "boolean", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiresManagerAttention = table.Column<bool>(type: "boolean", nullable: false),
                    ManagerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<int>(type: "integer", nullable: true),
                    RelatedEntityNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDraft = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInteractions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CustomerInteractions_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CustomerInteractionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerInteractionId = table.Column<int>(type: "integer", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    PreviousAssignedToUsername = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NewAssignedToUsername = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ChangedByUsername = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInteractionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInteractionHistories_CustomerInteractions_Customer~",
                        column: x => x.CustomerInteractionId,
                        principalTable: "CustomerInteractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractionHistories_CustomerInteractionId",
                table: "CustomerInteractionHistories",
                column: "CustomerInteractionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_AssignedToUserId",
                table: "CustomerInteractions",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_CustomerId",
                table: "CustomerInteractions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_FollowUpDate",
                table: "CustomerInteractions",
                column: "FollowUpDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_InteractionNumber",
                table: "CustomerInteractions",
                column: "InteractionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_NormalizedPhone",
                table: "CustomerInteractions",
                column: "NormalizedPhone");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_Status",
                table: "CustomerInteractions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerInteractionHistories");

            migrationBuilder.DropTable(
                name: "CustomerInteractions");
        }
    }
}
