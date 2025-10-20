using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyDescription",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CompanyEmail",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CompanyWebsite",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TwitterUrl",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyDescription",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "CompanyEmail",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "CompanyWebsite",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "LaundrySettings");

            migrationBuilder.DropColumn(
                name: "TwitterUrl",
                table: "LaundrySettings");
        }
    }
}
