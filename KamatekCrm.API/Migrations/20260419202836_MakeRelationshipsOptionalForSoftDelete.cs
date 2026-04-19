using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.API.Migrations
{
    /// <inheritdoc />
    public partial class MakeRelationshipsOptionalForSoftDelete : Migration
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

            migrationBuilder.AlterColumn<int>(
                name: "TaskId",
                table: "TaskPhotos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceJobId",
                table: "ServiceJobItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceJobId",
                table: "ServiceJobHistories",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

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
