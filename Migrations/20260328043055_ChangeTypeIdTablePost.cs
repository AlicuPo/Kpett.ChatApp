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

            migrationBuilder.DropColumn(
                name: "Id", schema: "KpettChatApp", table: "Posts");

            migrationBuilder.RenameColumn(
                name: "TempId", schema: "KpettChatApp", table: "Posts",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "Id", schema: "KpettChatApp", table: "Posts",
                type: "nvarchar(450)", nullable: false, defaultValue: "");

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
            migrationBuilder.AlterColumn<long>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "UserFeeds",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "KpettChatApp",
                table: "Posts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "PostReactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<long>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "PostMedia",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PostId",
                schema: "KpettChatApp",
                table: "Comments",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
