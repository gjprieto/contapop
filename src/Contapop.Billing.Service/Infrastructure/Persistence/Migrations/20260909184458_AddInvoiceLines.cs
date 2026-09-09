using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Billing.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "invoicing",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                schema: "invoicing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price_minor = table.Column<long>(type: "bigint", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    net_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    tax_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    total_amount_minor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "invoicing",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_invoice_id",
                schema: "invoicing",
                table: "invoice_lines",
                column: "invoice_id");

            migrationBuilder.Sql("""
                INSERT INTO invoicing.invoice_lines (id, invoice_id, description, quantity, unit_price_minor, tax_rate, net_amount_minor, tax_amount_minor, total_amount_minor)
                SELECT id, id, 'Invoice amount', 1, net_amount_minor, tax_rate, net_amount_minor, tax_amount_minor, total_amount_minor
                FROM invoicing.invoices;
                """);

            migrationBuilder.DropColumn(
                name: "tax_rate",
                schema: "invoicing",
                table: "invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_lines",
                schema: "invoicing");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "invoicing",
                table: "invoices");

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                schema: "invoicing",
                table: "invoices",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

        }
    }
}
