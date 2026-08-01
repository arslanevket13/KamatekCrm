using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KamatekCrm.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260801210000_EnforceTemporaryPasswordChange")]
public partial class EnforceTemporaryPasswordChange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MustChangePassword",
            table: "Users");
    }
}
