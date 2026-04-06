using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnPathInTableComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_Id",
                schema: "KpettChatApp",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationParticipants_User_Archived_Conversation",
                schema: "KpettChatApp",
                table: "ConversationParticipants");

            migrationBuilder.AddColumn<string>(
                name: "Path",
                schema: "KpettChatApp",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "KpettChatApp",
                table: "Blocks",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Path",
                schema: "KpettChatApp",
                table: "Comments");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "KpettChatApp",
                table: "Blocks",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

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
    }
}
