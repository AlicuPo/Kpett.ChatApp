using Kpett.ChatApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260624102000_AddMissingGroupMemberStatus")]
    public partial class AddMissingGroupMemberStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.GroupMembers', N'Status') IS NULL
BEGIN
    ALTER TABLE [GroupMembers] ADD [Status] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.GroupMembers', N'Status') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [GroupMembers] SET [Status] = N''active'' WHERE [Status] IS NULL;');
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.GroupMembers', N'Status') IS NOT NULL
BEGIN
    ALTER TABLE [GroupMembers] DROP COLUMN [Status];
END
""");
        }
    }
}
