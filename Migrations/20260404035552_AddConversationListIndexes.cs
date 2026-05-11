using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_Id",
                schema: "KpettChatApp",
                table: "Messages",
                columns: new[] { "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_User_Archived_Conversation",
                schema: "KpettChatApp",
                table: "ConversationParticipants",
                columns: new[] { "UserId", "IsArchived", "ConversationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_Id",
                schema: "KpettChatApp",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationParticipants_User_Archived_Conversation",
                schema: "KpettChatApp",
                table: "ConversationParticipants");
        }
    }
}
