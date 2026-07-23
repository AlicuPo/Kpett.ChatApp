using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClumnsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "Biography",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cocupation",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                schema: "KpettChatApp",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Interests",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccountPrivate",
                schema: "KpettChatApp",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                schema: "KpettChatApp",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialLinks",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Biography",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cocupation",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Interests",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAccountPrivate",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SocialLinks",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "KpettChatApp",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "KpettChatApp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
