using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class LaundryWorkflowUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAtRoomAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedBeaconMacAddress",
                table: "LaundryRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LaundryLoadedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumCharge",
                table: "LaundryRequests",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentCompletedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "LaundryRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PaymentNotes",
                table: "LaundryRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "LaundryRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentRequestedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerKg",
                table: "LaundryRequests",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedToBaseAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RobotDispatchedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomName",
                table: "LaundryRequests",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "WeighingCompletedAt",
                table: "LaundryRequests",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "ArrivedAtRoomAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "AssignedBeaconMacAddress",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "LaundryLoadedAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "MinimumCharge",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "PaymentCompletedAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "PaymentNotes",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "PaymentRequestedAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "PricePerKg",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "ReturnedToBaseAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "RobotDispatchedAt",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "RoomName",
                table: "LaundryRequests");

            migrationBuilder.DropColumn(
                name: "WeighingCompletedAt",
                table: "LaundryRequests");
        }
    }
}
