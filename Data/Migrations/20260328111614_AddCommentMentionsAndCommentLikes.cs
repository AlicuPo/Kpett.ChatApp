using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentMentionsAndCommentLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                schema: "KpettChatApp",
                table: "Comments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                schema: "KpettChatApp",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReplyCount",
                schema: "KpettChatApp",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE parent
                SET [ReplyCount] = replyCounts.[ReplyCount]
                FROM [KpettChatApp].[Comments] AS parent
                INNER JOIN (
                    SELECT [ParentCommentId], COUNT(1) AS [ReplyCount]
                    FROM [KpettChatApp].[Comments]
                    WHERE [ParentCommentId] IS NOT NULL
                      AND [DeletedAt] IS NULL
                    GROUP BY [ParentCommentId]
                ) AS replyCounts
                    ON parent.[Id] = replyCounts.[ParentCommentId];
                """);

            migrationBuilder.CreateTable(
                name: "CommentLikes",
                schema: "KpettChatApp",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CommentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentLikes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MentionComments",
                schema: "KpettChatApp",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CommentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsNotified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentionComments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_CommentId",
                schema: "KpettChatApp",
                table: "CommentLikes",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_CommentId_UserId",
                schema: "KpettChatApp",
                table: "CommentLikes",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_UserId",
                schema: "KpettChatApp",
                table: "CommentLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentionComments_CommentId",
                schema: "KpettChatApp",
                table: "MentionComments",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_MentionComments_CommentId_UserId",
                schema: "KpettChatApp",
                table: "MentionComments",
                columns: new[] { "CommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentionComments_UserId",
                schema: "KpettChatApp",
                table: "MentionComments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentLikes",
                schema: "KpettChatApp");

            migrationBuilder.DropTable(
                name: "MentionComments",
                schema: "KpettChatApp");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                schema: "KpettChatApp",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "LikeCount",
                schema: "KpettChatApp",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ReplyCount",
                schema: "KpettChatApp",
                table: "Comments");
        }
    }
}
