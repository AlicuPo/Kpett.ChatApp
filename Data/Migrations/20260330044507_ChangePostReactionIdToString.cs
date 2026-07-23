using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangePostReactionIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostReactions_Temp",
                schema: "KpettChatApp",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PostId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReactions_Temp", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO [KpettChatApp].[PostReactions_Temp] ([Id], [PostId], [UserId], [Type], [CreatedAt])
                SELECT CONVERT(nvarchar(450), [Id]), [PostId], [UserId], [Type], [CreatedAt]
                FROM [KpettChatApp].[PostReactions];
                """);

            migrationBuilder.DropTable(
                name: "PostReactions",
                schema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "PostReactions_Temp",
                schema: "KpettChatApp",
                newName: "PostReactions",
                newSchema: "KpettChatApp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [KpettChatApp].[PostReactions]
                    WHERE TRY_CONVERT(bigint, [Id]) IS NULL
                )
                BEGIN
                    THROW 50000, 'Cannot downgrade PostReactions.Id to bigint because the table contains non-numeric IDs.', 1;
                END
                """);

            migrationBuilder.CreateTable(
                name: "PostReactions_Temp",
                schema: "KpettChatApp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReactions_Temp", x => x.Id);
                });

            migrationBuilder.Sql("""
                SET IDENTITY_INSERT [KpettChatApp].[PostReactions_Temp] ON;

                INSERT INTO [KpettChatApp].[PostReactions_Temp] ([Id], [PostId], [UserId], [Type], [CreatedAt])
                SELECT CONVERT(bigint, [Id]), [PostId], [UserId], [Type], [CreatedAt]
                FROM [KpettChatApp].[PostReactions];

                SET IDENTITY_INSERT [KpettChatApp].[PostReactions_Temp] OFF;
                """);

            migrationBuilder.DropTable(
                name: "PostReactions",
                schema: "KpettChatApp");

            migrationBuilder.RenameTable(
                name: "PostReactions_Temp",
                schema: "KpettChatApp",
                newName: "PostReactions",
                newSchema: "KpettChatApp");
        }
    }
}
