using Kpett.ChatApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260623103000_AddMissingGroupOwnerAndStatus")]
    public partial class AddMissingGroupOwnerAndStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Groups', N'OwnerUserId') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [OwnerUserId] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'Status') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [Status] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'OwnerUserId') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [Groups] SET [OwnerUserId] = [CreatedByUserId] WHERE [OwnerUserId] IS NULL;');
END

IF COL_LENGTH(N'dbo.Groups', N'Status') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [Groups] SET [Status] = N''active'' WHERE [Status] IS NULL;');
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Groups', N'OwnerUserId') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [OwnerUserId];
END

IF COL_LENGTH(N'dbo.Groups', N'Status') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [Status];
END
""");
        }
    }
}
