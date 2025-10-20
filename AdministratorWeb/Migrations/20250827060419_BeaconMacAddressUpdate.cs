using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class BeaconMacAddressUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_BluetoothBeacons_AssignedBeaconId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssignedBeaconId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssignedBeaconId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "AssignedBeaconMacAddress",
                table: "AspNetUsers",
                type: "varchar(17)",
                maxLength: 17,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedBeaconMacAddress",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "AssignedBeaconId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

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
    }
}
