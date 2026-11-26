using System;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261123210000_AddMultiLayerInteractionFields")]
    public partial class AddMultiLayerInteractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""Direction"" integer NOT NULL DEFAULT 1;
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""BallInCourt"" integer NOT NULL DEFAULT 1;
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""SnoozedUntilUtc"" timestamp with time zone NULL;
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""LinkedEntityType"" character varying(50) NULL;
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""LinkedEntityId"" integer NULL;
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""EntriesJson"" jsonb NOT NULL DEFAULT '[]'::jsonb;
                ALTER TABLE ""CustomerInteractions"" ADD COLUMN IF NOT EXISTS ""ChecklistJson"" jsonb NOT NULL DEFAULT '[]'::jsonb;

                CREATE INDEX IF NOT EXISTS ""IX_CustomerInteractions_Direction"" ON ""CustomerInteractions"" (""Direction"");
                CREATE INDEX IF NOT EXISTS ""IX_CustomerInteractions_BallInCourt"" ON ""CustomerInteractions"" (""BallInCourt"");
                CREATE INDEX IF NOT EXISTS ""IX_CustomerInteractions_SnoozedUntilUtc"" ON ""CustomerInteractions"" (""SnoozedUntilUtc"");
            ");

            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""IsOnDuty"" boolean NOT NULL DEFAULT FALSE;");
            migrationBuilder.Sql(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""TotalJobsCompleted"" integer NOT NULL DEFAULT 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_CustomerInteractions_Direction"";
                DROP INDEX IF EXISTS ""IX_CustomerInteractions_BallInCourt"";
                DROP INDEX IF EXISTS ""IX_CustomerInteractions_SnoozedUntilUtc"";

                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""Direction"";
                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""BallInCourt"";
                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""SnoozedUntilUtc"";
                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""LinkedEntityType"";
                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""LinkedEntityId"";
                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""EntriesJson"";
                ALTER TABLE ""CustomerInteractions"" DROP COLUMN IF EXISTS ""ChecklistJson"";

                ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""IsOnDuty"";
                ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""TotalJobsCompleted"";
            ");
        }
    }
}
