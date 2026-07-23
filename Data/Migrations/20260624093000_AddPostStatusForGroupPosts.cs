using Kpett.ChatApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260624093000_AddPostStatusForGroupPosts")]
    public partial class AddPostStatusForGroupPosts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Posts', N'Status') IS NULL
BEGIN
    ALTER TABLE [Posts] ADD [Status] nvarchar(32) NULL;
END

IF COL_LENGTH(N'dbo.Posts', N'Status') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [Posts] SET [Status] = N''approved'' WHERE [Status] IS NULL;');
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Posts_Group_Deleted_Status_CreatedAt_Id'
      AND [object_id] = OBJECT_ID(N'dbo.Posts')
)
BEGIN
    CREATE INDEX [IX_Posts_Group_Deleted_Status_CreatedAt_Id]
    ON [Posts] ([GroupId], [IsDeleted], [Status], [CreatedAt], [Id]);
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Posts_Group_Deleted_Status_CreatedAt_Id'
      AND [object_id] = OBJECT_ID(N'dbo.Posts')
)
BEGIN
    DROP INDEX [IX_Posts_Group_Deleted_Status_CreatedAt_Id] ON [Posts];
END

IF COL_LENGTH(N'dbo.Posts', N'Status') IS NOT NULL
BEGIN
    ALTER TABLE [Posts] DROP COLUMN [Status];
END
""");
        }
    }
}
