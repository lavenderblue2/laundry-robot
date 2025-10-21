using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomArrivalTimeoutSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomArrivalTimeoutMinutes",
                table: "LaundrySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoomArrivalTimeoutMinutes",
                table: "LaundrySettings");
        }
    }
}
