using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class Migration_10397 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LaundryRequests_AspNetUsers_HandledByUserId",
                table: "LaundryRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LaundryRequests_LaundryRobots_AssignedRobotId",
                table: "LaundryRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LaundryRobots",
                table: "LaundryRobots");

            migrationBuilder.RenameTable(
                name: "LaundryRobots",
                newName: "LaundryRobot");

            migrationBuilder.RenameColumn(
                name: "HandledByUserId",
                table: "LaundryRequests",
                newName: "HandledById");

            migrationBuilder.RenameIndex(
                name: "IX_LaundryRequests_HandledByUserId",
                table: "LaundryRequests",
                newName: "IX_LaundryRequests_HandledById");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentTask",
                table: "LaundryRobot",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "LaundryRobot",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LaundryRobot",
                table: "LaundryRobot",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LaundryRequests_AspNetUsers_HandledById",
                table: "LaundryRequests",
                column: "HandledById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LaundryRequests_LaundryRobot_AssignedRobotId",
                table: "LaundryRequests",
                column: "AssignedRobotId",
                principalTable: "LaundryRobot",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LaundryRequests_AspNetUsers_HandledById",
                table: "LaundryRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_LaundryRequests_LaundryRobot_AssignedRobotId",
                table: "LaundryRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LaundryRobot",
                table: "LaundryRobot");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "CurrentTask",
                table: "LaundryRobot");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LaundryRobot");

            migrationBuilder.RenameTable(
                name: "LaundryRobot",
                newName: "LaundryRobots");

            migrationBuilder.RenameColumn(
                name: "HandledById",
                table: "LaundryRequests",
                newName: "HandledByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_LaundryRequests_HandledById",
                table: "LaundryRequests",
                newName: "IX_LaundryRequests_HandledByUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LaundryRobots",
                table: "LaundryRobots",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LaundryRequests_AspNetUsers_HandledByUserId",
                table: "LaundryRequests",
                column: "HandledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LaundryRequests_LaundryRobots_AssignedRobotId",
                table: "LaundryRequests",
                column: "AssignedRobotId",
                principalTable: "LaundryRobots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
