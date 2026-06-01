using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class CreateMessageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "KpettChatApp",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SenderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReplyToMessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientMessageId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages",
                schema: "KpettChatApp");
        }
    }
}
