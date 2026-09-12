using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Billing.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArchivedInvoiceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_status_date",
                schema: "invoicing",
                table: "invoices",
                columns: new[] { "tenant_id", "status", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_tenant_id_status_date",
                schema: "invoicing",
                table: "invoices");
        }
    }
}
