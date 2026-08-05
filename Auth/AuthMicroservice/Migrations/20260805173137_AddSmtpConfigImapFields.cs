using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthMicroservice.Migrations
{
    /// <inheritdoc />
    public partial class AddSmtpConfigImapFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImapEnableSsl",
                table: "SmtpConfigs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ImapHost",
                table: "SmtpConfigs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ImapPort",
                table: "SmtpConfigs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastProcessedImapUid",
                table: "SmtpConfigs",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImapEnableSsl",
                table: "SmtpConfigs");

            migrationBuilder.DropColumn(
                name: "ImapHost",
                table: "SmtpConfigs");

            migrationBuilder.DropColumn(
                name: "ImapPort",
                table: "SmtpConfigs");

            migrationBuilder.DropColumn(
                name: "LastProcessedImapUid",
                table: "SmtpConfigs");
        }
    }
}
