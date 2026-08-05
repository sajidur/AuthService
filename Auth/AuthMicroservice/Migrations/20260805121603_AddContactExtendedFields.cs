using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthMicroservice.Migrations
{
    /// <inheritdoc />
    public partial class AddContactExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Division",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExtraData",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Gps",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HealthData",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HomeAddress",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Nid",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Thana",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "WorkAddress",
                table: "Contacts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "District",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Division",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ExtraData",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Gps",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "HealthData",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "HomeAddress",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Nid",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Thana",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "WorkAddress",
                table: "Contacts");
        }
    }
}
