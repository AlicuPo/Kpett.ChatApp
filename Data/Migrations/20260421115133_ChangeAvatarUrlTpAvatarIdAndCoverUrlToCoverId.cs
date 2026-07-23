using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAvatarUrlTpAvatarIdAndCoverUrlToCoverId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CoverUrl",
                schema: "KpettChatApp",
                table: "Users",
                newName: "CoverId");

            migrationBuilder.RenameColumn(
                name: "AvatarUrl",
                schema: "KpettChatApp",
                table: "Users",
                newName: "AvatarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CoverId",
                schema: "KpettChatApp",
                table: "Users",
                newName: "CoverUrl");

            migrationBuilder.RenameColumn(
                name: "AvatarId",
                schema: "KpettChatApp",
                table: "Users",
                newName: "AvatarUrl");
        }
    }
}
