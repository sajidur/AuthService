using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthMicroservice.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyImapConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapHost",
                table: "SmtpConfigs");

            migrationBuilder.AddColumn<bool>(
                name: "ImapEnabled",
                table: "SmtpConfigs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapEnabled",
                table: "SmtpConfigs");

            migrationBuilder.AddColumn<string>(
                name: "ImapHost",
                table: "SmtpConfigs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
