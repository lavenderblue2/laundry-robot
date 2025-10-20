using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAwaitingAdminLoadStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineFollowColorB",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "LineFollowColorG",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "LineFollowColorR",
                table: "LaundrySettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "LineFollowColorB",
                table: "LaundrySettings",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "LineFollowColorG",
                table: "LaundrySettings",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "LineFollowColorR",
                table: "LaundrySettings",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);
        }
    }
}
