using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTypeIdTablePost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "UserFeeds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "TempId", schema: "KpettChatApp", table: "Posts",
                type: "nvarchar(450)", nullable: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_Posts",
                schema: "KpettChatApp",
                table: "Posts");

            migrationBuilder.Sql("""
                UPDATE [KpettChatApp].[Posts]
                SET [TempId] = CONVERT(nvarchar(450), [Id])
                WHERE [TempId] IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Id", schema: "KpettChatApp", table: "Posts");

            migrationBuilder.RenameColumn(
                name: "TempId", schema: "KpettChatApp", table: "Posts",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "Id", schema: "KpettChatApp", table: "Posts",
                type: "nvarchar(450)", nullable: false, defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Posts",
                schema: "KpettChatApp",
                table: "Posts",
                column: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "PostReactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "PostMedia",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Rollback for migration '20260328043055_ChangeTypeIdTablePost' is not supported.");
        }
    }
}
