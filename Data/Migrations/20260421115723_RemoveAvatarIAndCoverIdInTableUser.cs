using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAvatarIAndCoverIdInTableUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarId",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CoverId",
                schema: "KpettChatApp",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarId",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverId",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
