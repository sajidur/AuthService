using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthMicroservice.Migrations
{
    /// <inheritdoc />
    public partial class AddPerAppJwtAudienceIssuer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "Applications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "Applications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Backfill existing tenants (created before per-app Audience/Issuer existed) so
            // their already-issued tokens' aud/iss keep validating — same default used for
            // new tenants in ApplicationService.RegisterApplicationAsync (AppKey is already
            // a unique per-tenant value).
            migrationBuilder.Sql("UPDATE `Applications` SET `Audience` = `AppKey`, `Issuer` = `AppKey` WHERE `Audience` IS NULL OR `Issuer` IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "Applications");
        }
    }
}
