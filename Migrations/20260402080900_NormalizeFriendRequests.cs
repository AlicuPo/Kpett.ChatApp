using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeFriendRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "KpettChatApp",
                table: "FriendRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserHighId",
                schema: "KpettChatApp",
                table: "FriendRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserLowId",
                schema: "KpettChatApp",
                table: "FriendRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [KpettChatApp].[FriendRequests]
                SET
                    [UserLowId] = CASE
                        WHEN [SenderId] < [ReceiverId] THEN [SenderId]
                        ELSE [ReceiverId]
                    END,
                    [UserHighId] = CASE
                        WHEN [SenderId] < [ReceiverId] THEN [ReceiverId]
                        ELSE [SenderId]
                    END
                WHERE [UserLowId] IS NULL
                    OR [UserHighId] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                ;WITH RankedRequests AS
                (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY [UserLowId], [UserHighId]
                            ORDER BY
                                CASE WHEN [Status] = N'Pending' THEN 0 ELSE 1 END,
                                COALESCE([UpdatedAt], [CreatedAt], CONVERT(datetime2, '0001-01-01T00:00:00')) DESC,
                                [CreatedAt] DESC,
                                [Id] DESC
                        ) AS [RowNumber]
                    FROM [KpettChatApp].[FriendRequests]
                )
                DELETE FROM [KpettChatApp].[FriendRequests]
                WHERE [Id] IN
                (
                    SELECT [Id]
                    FROM RankedRequests
                    WHERE [RowNumber] > 1
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "UserHighId",
                schema: "KpettChatApp",
                table: "FriendRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserLowId",
                schema: "KpettChatApp",
                table: "FriendRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Receiver_Status_CreatedAt",
                schema: "KpettChatApp",
                table: "FriendRequests",
                columns: new[] { "ReceiverId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Sender_Status_CreatedAt",
                schema: "KpettChatApp",
                table: "FriendRequests",
                columns: new[] { "SenderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_UserPair",
                schema: "KpettChatApp",
                table: "FriendRequests",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Receiver_Status_CreatedAt",
                schema: "KpettChatApp",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Sender_Status_CreatedAt",
                schema: "KpettChatApp",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_UserPair",
                schema: "KpettChatApp",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "UserHighId",
                schema: "KpettChatApp",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "UserLowId",
                schema: "KpettChatApp",
                table: "FriendRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "KpettChatApp",
                table: "FriendRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
