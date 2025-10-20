using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class Migration_13817 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LaundryRequests_LaundryRobot_AssignedRobotId",
                table: "LaundryRequests");

            migrationBuilder.DropTable(
                name: "LaundryRobot");

            migrationBuilder.DropIndex(
                name: "IX_LaundryRequests_AssignedRobotId",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "AssignedRobotId",
                table: "LaundryRequests");

            migrationBuilder.AddColumn<string>(
                name: "AssignedRobotName",
                table: "LaundryRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedRobotName",
                table: "LaundryRequests");

            migrationBuilder.AddColumn<int>(
                name: "AssignedRobotId",
                table: "LaundryRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LaundryRobot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CanAcceptRequests = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CurrentLocation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentTask = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DomainName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaundryRobot", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryRequests_AssignedRobotId",
                table: "LaundryRequests",
                column: "AssignedRobotId");

            migrationBuilder.AddForeignKey(
                name: "FK_LaundryRequests_LaundryRobot_AssignedRobotId",
                table: "LaundryRequests",
                column: "AssignedRobotId",
                principalTable: "LaundryRobot",
                principalColumn: "Id");
        }
    }
}
