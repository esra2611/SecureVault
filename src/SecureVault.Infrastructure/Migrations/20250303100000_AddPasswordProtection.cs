using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPasswordProtected",
                table: "Secrets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHashBase64",
                table: "Secrets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPasswordProtected",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "PasswordHashBase64",
                table: "Secrets");
        }
    }
}
