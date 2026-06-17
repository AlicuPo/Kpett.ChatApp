using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "Groups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Groups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MemberApproval",
                table: "Groups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PostApproval",
                table: "Groups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhoCanInvite",
                table: "Groups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhoCanPost",
                table: "Groups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupInvitations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InviteeUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupNotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupPosts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TopicId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupStats",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MemberCount = table.Column<int>(type: "int", nullable: false),
                    PostCount = table.Column<int>(type: "int", nullable: false),
                    InteractionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupStats", x => new { x.GroupId, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "GroupTopics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupTopics", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupInvitations");

            migrationBuilder.DropTable(
                name: "GroupNotificationSettings");

            migrationBuilder.DropTable(
                name: "GroupPosts");

            migrationBuilder.DropTable(
                name: "GroupRules");

            migrationBuilder.DropTable(
                name: "GroupStats");

            migrationBuilder.DropTable(
                name: "GroupTopics");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "MemberApproval",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "PostApproval",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "WhoCanInvite",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "WhoCanPost",
                table: "Groups");
        }
    }
}
