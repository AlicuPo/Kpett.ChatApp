using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class ChangeLastMessageIdNullAbleInPaticipanTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[KpettChatApp].[MessageDetail]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [KpettChatApp].[MessageDetail];
                END;

                IF OBJECT_ID(N'[KpettChatApp].[MessageDetails]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [KpettChatApp].[MessageDetails];
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LastReadMessageId",
                schema: "KpettChatApp",
                table: "ConversationParticipants",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LastReadMessageId",
                schema: "KpettChatApp",
                table: "ConversationParticipants",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "MessageDetail",
                schema: "KpettChatApp",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageDetail", x => x.MessageId);
                });
        }
    }
}
