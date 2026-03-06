using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureVault.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "AuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_MessageId",
                table: "AuditLogs",
                column: "MessageId",
                unique: true,
                filter: "\"MessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_MessageId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "AuditLogs");
        }
    }
}
