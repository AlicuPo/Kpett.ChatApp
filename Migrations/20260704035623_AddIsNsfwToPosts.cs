using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class AddIsNsfwToPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNsfw",
                table: "Posts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNsfw",
                table: "Posts");
        }
    }
}
