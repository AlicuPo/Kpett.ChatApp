using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeUserPostsListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Posts_User_Deleted_PinnedAt_CreatedAt_Id",
                schema: "KpettChatApp",
                table: "Posts",
                columns: new[] { "CreatedByUserId", "IsDeleted", "PinnedAt", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_PostId",
                schema: "KpettChatApp",
                table: "PostReactions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReactions_PostId_UserId",
                schema: "KpettChatApp",
                table: "PostReactions",
                columns: new[] { "PostId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId",
                schema: "KpettChatApp",
                table: "Comments",
                column: "PostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_User_Deleted_PinnedAt_CreatedAt_Id",
                schema: "KpettChatApp",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_PostReactions_PostId",
                schema: "KpettChatApp",
                table: "PostReactions");

            migrationBuilder.DropIndex(
                name: "IX_PostReactions_PostId_UserId",
                schema: "KpettChatApp",
                table: "PostReactions");

            migrationBuilder.DropIndex(
                name: "IX_Comments_PostId",
                schema: "KpettChatApp",
                table: "Comments");
        }
    }
}
