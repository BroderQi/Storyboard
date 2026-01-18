using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storyboard.Migrations
{
    /// <inheritdoc />
    public partial class AddCreativeIntentToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreativeGoal",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetAudience",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoTone",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyMessage",
                table: "Projects",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreativeGoal",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VideoTone",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "KeyMessage",
                table: "Projects");
        }
    }
}
