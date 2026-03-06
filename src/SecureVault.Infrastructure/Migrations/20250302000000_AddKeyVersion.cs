using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KeyVersion",
                table: "Secrets",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyVersion",
                table: "Secrets");
        }
    }
}
