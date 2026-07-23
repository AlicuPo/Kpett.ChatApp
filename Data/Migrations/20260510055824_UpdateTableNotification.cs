using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTableNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                schema: "KpettChatApp",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Data",
                schema: "KpettChatApp",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "KpettChatApp",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                schema: "KpettChatApp",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "KpettChatApp",
                table: "Notifications",
                newName: "RecipientId");

            migrationBuilder.RenameColumn(
                name: "Title",
                schema: "KpettChatApp",
                table: "Notifications",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                schema: "KpettChatApp",
                table: "Notifications",
                newName: "ReferenceId");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "KpettChatApp",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "ReferenceId",
                schema: "KpettChatApp",
                table: "Notifications",
                newName: "SenderId");

            migrationBuilder.RenameColumn(
                name: "RecipientId",
                schema: "KpettChatApp",
                table: "Notifications",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Metadata",
                schema: "KpettChatApp",
                table: "Notifications",
                newName: "Title");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRead",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Data",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                schema: "KpettChatApp",
                table: "Notifications",
                type: "datetime2",
                nullable: true);
        }
    }
}
