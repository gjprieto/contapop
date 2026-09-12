using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Billing.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachment_cleanups",
                schema: "invoicing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blob_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachment_cleanups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_attachments",
                schema: "invoicing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blob_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_attachments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attachment_cleanups_next_attempt_at_attempt_count",
                schema: "invoicing",
                table: "attachment_cleanups",
                columns: new[] { "next_attempt_at", "attempt_count" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_attachments_invoice_id",
                schema: "invoicing",
                table: "invoice_attachments",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_attachments_tenant_id_blob_name",
                schema: "invoicing",
                table: "invoice_attachments",
                columns: new[] { "tenant_id", "blob_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_cleanups",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "invoice_attachments",
                schema: "invoicing");
        }
    }
}
