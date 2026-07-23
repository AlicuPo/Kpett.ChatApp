using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "KpettChatApp",
                table: "Friendships",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserHigh_Status_CreatedAt_UserLow",
                schema: "KpettChatApp",
                table: "Friendships",
                columns: new[] { "UserHighId", "Status", "CreatedAt", "UserLowId" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserLow_Status_CreatedAt_UserHigh",
                schema: "KpettChatApp",
                table: "Friendships",
                columns: new[] { "UserLowId", "Status", "CreatedAt", "UserHighId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_UserHigh_Status_CreatedAt_UserLow",
                schema: "KpettChatApp",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_UserLow_Status_CreatedAt_UserHigh",
                schema: "KpettChatApp",
                table: "Friendships");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "KpettChatApp",
                table: "Friendships",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
