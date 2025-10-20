using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class BeaconAssignmentUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBase",
                table: "BluetoothBeacons",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AssignedBeaconId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomDescription",
                table: "AspNetUsers",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RoomNumber",
                table: "AspNetUsers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BluetoothBeacons_IsBase",
                table: "BluetoothBeacons",
                column: "IsBase");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AssignedBeaconId",
                table: "AspNetUsers",
                column: "AssignedBeaconId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_BluetoothBeacons_AssignedBeaconId",
                table: "AspNetUsers",
                column: "AssignedBeaconId",
                principalTable: "BluetoothBeacons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_BluetoothBeacons_AssignedBeaconId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_BluetoothBeacons_IsBase",
                table: "BluetoothBeacons");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssignedBeaconId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsBase",
                table: "BluetoothBeacons");

            migrationBuilder.DropColumn(
                name: "AssignedBeaconId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RoomDescription",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "AspNetUsers");
        }
    }
}
