using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class RemoveChema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UserSettings",
                schema: "KpettChatApp",
                newName: "UserSettings");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "KpettChatApp",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "UserMedias",
                schema: "KpettChatApp",
                newName: "UserMedias");

            migrationBuilder.RenameTable(
                name: "UserFeeds",
                schema: "KpettChatApp",
                newName: "UserFeeds");

            migrationBuilder.RenameTable(
                name: "UserDevices",
                schema: "KpettChatApp",
                newName: "UserDevices");

            migrationBuilder.RenameTable(
                name: "Posts",
                schema: "KpettChatApp",
                newName: "Posts");

            migrationBuilder.RenameTable(
                name: "PostReactionTypes",
                schema: "KpettChatApp",
                newName: "PostReactionTypes");

            migrationBuilder.RenameTable(
                name: "PostReactions",
                schema: "KpettChatApp",
                newName: "PostReactions");

            migrationBuilder.RenameTable(
                name: "PostMedia",
                schema: "KpettChatApp",
                newName: "PostMedia");

            migrationBuilder.RenameTable(
                name: "Notifications",
                schema: "KpettChatApp",
                newName: "Notifications");

            migrationBuilder.RenameTable(
                name: "Messages",
                schema: "KpettChatApp",
                newName: "Messages");

            migrationBuilder.RenameTable(
                name: "MentionComments",
                schema: "KpettChatApp",
                newName: "MentionComments");

            migrationBuilder.RenameTable(
                name: "Groups",
                schema: "KpettChatApp",
                newName: "Groups");

            migrationBuilder.RenameTable(
                name: "GroupMembers",
                schema: "KpettChatApp",
                newName: "GroupMembers");

            migrationBuilder.RenameTable(
                name: "Friendships",
                schema: "KpettChatApp",
                newName: "Friendships");

            migrationBuilder.RenameTable(
                name: "FriendRequests",
                schema: "KpettChatApp",
                newName: "FriendRequests");

            migrationBuilder.RenameTable(
                name: "Follows",
                schema: "KpettChatApp",
                newName: "Follows");

            migrationBuilder.RenameTable(
                name: "Conversations",
                schema: "KpettChatApp",
                newName: "Conversations");

            migrationBuilder.RenameTable(
                name: "ConversationParticipants",
                schema: "KpettChatApp",
                newName: "ConversationParticipants");

            migrationBuilder.RenameTable(
                name: "ConversationKeys",
                schema: "KpettChatApp",
                newName: "ConversationKeys");

            migrationBuilder.RenameTable(
                name: "Comments",
                schema: "KpettChatApp",
                newName: "Comments");

            migrationBuilder.RenameTable(
                name: "CommentLikes",
                schema: "KpettChatApp",
                newName: "CommentLikes");

            migrationBuilder.RenameTable(
                name: "Blocks",
                schema: "KpettChatApp",
                newName: "Blocks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "UserSettings",
                newName: "UserSettings",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "UserMedias",
                newName: "UserMedias",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "UserFeeds",
                newName: "UserFeeds",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "UserDevices",
                newName: "UserDevices",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Posts",
                newName: "Posts",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "PostReactionTypes",
                newName: "PostReactionTypes",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "PostReactions",
                newName: "PostReactions",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "PostMedia",
                newName: "PostMedia",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Notifications",
                newName: "Notifications",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Messages",
                newName: "Messages",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "MentionComments",
                newName: "MentionComments",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Groups",
                newName: "Groups",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "GroupMembers",
                newName: "GroupMembers",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Friendships",
                newName: "Friendships",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "FriendRequests",
                newName: "FriendRequests",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Follows",
                newName: "Follows",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Conversations",
                newName: "Conversations",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "ConversationParticipants",
                newName: "ConversationParticipants",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "ConversationKeys",
                newName: "ConversationKeys",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Comments",
                newName: "Comments",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "CommentLikes",
                newName: "CommentLikes",
                newSchema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "Blocks",
                newName: "Blocks",
                newSchema: "KpettChatApp");
        }
    }
}
