using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SealAuditLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntegrityHash",
                table: "ActivityLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IntegrityVersion",
                table: "ActivityLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_IntegrityHash",
                table: "ActivityLogs",
                column: "IntegrityHash");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "PreventActivityLogMutation"()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'ActivityLogs is append-only; UPDATE and DELETE are prohibited.'
                        USING ERRCODE = '55000';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_ActivityLogs_AppendOnly"
                BEFORE UPDATE OR DELETE ON "ActivityLogs"
                FOR EACH ROW EXECUTE FUNCTION "PreventActivityLogMutation"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS "TR_ActivityLogs_AppendOnly" ON "ActivityLogs";
                DROP FUNCTION IF EXISTS "PreventActivityLogMutation"();
                """);

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_IntegrityHash",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "IntegrityVersion",
                table: "ActivityLogs");
        }
    }
}
