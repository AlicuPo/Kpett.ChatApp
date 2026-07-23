using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupTablesNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Groups', N'CoverUrl') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [CoverUrl] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'Language') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [Language] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'MemberApproval') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [MemberApproval] bit NOT NULL CONSTRAINT [DF_Groups_MemberApproval] DEFAULT CAST(0 AS bit);
END

IF COL_LENGTH(N'dbo.Groups', N'OwnerUserId') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [OwnerUserId] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'PostApproval') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [PostApproval] bit NOT NULL CONSTRAINT [DF_Groups_PostApproval] DEFAULT CAST(0 AS bit);
END

IF COL_LENGTH(N'dbo.Groups', N'Status') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [Status] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'WhoCanInvite') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [WhoCanInvite] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'WhoCanPost') IS NULL
BEGIN
    ALTER TABLE [Groups] ADD [WhoCanPost] nvarchar(max) NULL;
END

IF COL_LENGTH(N'dbo.Groups', N'Status') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [Groups] SET [Status] = N''active'' WHERE [Status] IS NULL;');
END

IF COL_LENGTH(N'dbo.Groups', N'OwnerUserId') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [Groups] SET [OwnerUserId] = [CreatedByUserId] WHERE [OwnerUserId] IS NULL;');
END

IF OBJECT_ID(N'dbo.GroupInvitations', N'U') IS NULL
BEGIN
    CREATE TABLE [GroupInvitations] (
        [Id] nvarchar(450) NOT NULL,
        [GroupId] nvarchar(450) NOT NULL,
        [InvitedByUserId] nvarchar(450) NOT NULL,
        [InviteeUserId] nvarchar(450) NOT NULL,
        [Status] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GroupInvitations] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'dbo.GroupRules', N'U') IS NULL
BEGIN
    CREATE TABLE [GroupRules] (
        [Id] nvarchar(450) NOT NULL,
        [GroupId] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [Order] int NOT NULL,
        CONSTRAINT [PK_GroupRules] PRIMARY KEY ([Id])
    );
END
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID(N'dbo.GroupRules', N'U') IS NOT NULL
BEGIN
    DROP TABLE [GroupRules];
END

IF OBJECT_ID(N'dbo.GroupInvitations', N'U') IS NOT NULL
BEGIN
    DROP TABLE [GroupInvitations];
END

IF COL_LENGTH(N'dbo.Groups', N'WhoCanPost') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [WhoCanPost];
END

IF COL_LENGTH(N'dbo.Groups', N'WhoCanInvite') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [WhoCanInvite];
END

IF COL_LENGTH(N'dbo.Groups', N'Status') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [Status];
END

IF COL_LENGTH(N'dbo.Groups', N'PostApproval') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP CONSTRAINT IF EXISTS [DF_Groups_PostApproval];
    ALTER TABLE [Groups] DROP COLUMN [PostApproval];
END

IF COL_LENGTH(N'dbo.Groups', N'OwnerUserId') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [OwnerUserId];
END

IF COL_LENGTH(N'dbo.Groups', N'MemberApproval') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP CONSTRAINT IF EXISTS [DF_Groups_MemberApproval];
    ALTER TABLE [Groups] DROP COLUMN [MemberApproval];
END

IF COL_LENGTH(N'dbo.Groups', N'Language') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [Language];
END

IF COL_LENGTH(N'dbo.Groups', N'CoverUrl') IS NOT NULL
BEGIN
    ALTER TABLE [Groups] DROP COLUMN [CoverUrl];
END
""");

        }
    }
}
