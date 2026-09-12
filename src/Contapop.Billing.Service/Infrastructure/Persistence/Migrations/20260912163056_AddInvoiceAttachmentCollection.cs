using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Billing.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAttachmentCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoice_attachments_invoice_id",
                schema: "invoicing",
                table: "invoice_attachments");

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "invoicing",
                table: "invoice_attachments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "invoice");

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "invoicing",
                table: "invoice_attachments",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_attachments_invoice_id",
                schema: "invoicing",
                table: "invoice_attachments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_attachments_invoice_id_type",
                schema: "invoicing",
                table: "invoice_attachments",
                columns: new[] { "invoice_id", "type" },
                unique: true,
                filter: "type = 'invoice'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoice_attachments_invoice_id",
                schema: "invoicing",
                table: "invoice_attachments");

            migrationBuilder.DropIndex(
                name: "IX_invoice_attachments_invoice_id_type",
                schema: "invoicing",
                table: "invoice_attachments");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "invoicing",
                table: "invoice_attachments");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "invoicing",
                table: "invoice_attachments");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_attachments_invoice_id",
                schema: "invoicing",
                table: "invoice_attachments",
                column: "invoice_id",
                unique: true);
        }
    }
}
