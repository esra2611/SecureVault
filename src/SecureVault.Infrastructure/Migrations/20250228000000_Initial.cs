using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Secrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHashBase64 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiryType = table.Column<int>(type: "integer", nullable: false),
                    UtcCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UtcExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UtcRevealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    Nonce = table.Column<byte[]>(type: "bytea", maxLength: 12, nullable: true),
                    SaltForPassword = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secrets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_TokenHashBase64",
                table: "Secrets",
                column: "TokenHashBase64",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Secrets");
        }
    }
}
