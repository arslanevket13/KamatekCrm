using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KamatekCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveryFieldsToServiceJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""DiscoveryTechnicalNotes"" text;
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""EstimatedLaborHours"" double precision NOT NULL DEFAULT 0;
                ALTER TABLE ""ServiceJobs"" ADD COLUMN IF NOT EXISTS ""IsConvertedToQuote"" boolean NOT NULL DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""DiscoveryTechnicalNotes"";
                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""EstimatedLaborHours"";
                ALTER TABLE ""ServiceJobs"" DROP COLUMN IF EXISTS ""IsConvertedToQuote"";
            ");
        }
    }
}
