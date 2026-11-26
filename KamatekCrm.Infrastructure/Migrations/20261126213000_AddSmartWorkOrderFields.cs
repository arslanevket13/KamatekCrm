using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261126213000_AddSmartWorkOrderFields")]
    public partial class AddSmartWorkOrderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""SubTasksJson"" jsonb NOT NULL DEFAULT '[]'::jsonb;
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""NextFollowUpUtc"" timestamp with time zone NULL;
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""FollowUpNote"" text NULL;
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""DiscoveryChecklistJson"" jsonb NOT NULL DEFAULT '[]'::jsonb;

                CREATE INDEX IF NOT EXISTS ""IX_ServiceJobs_NextFollowUpUtc"" ON ""ServiceJobs"" (""NextFollowUpUtc"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_ServiceJobs_NextFollowUpUtc"";

                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""DiscoveryChecklistJson"";
                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""FollowUpNote"";
                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""NextFollowUpUtc"";
                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""SubTasksJson"";
            ");
        }
    }
}
