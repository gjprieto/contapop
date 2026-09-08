using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Ledger.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reconciliation_claims",
                schema: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependent_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dependent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_claims", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_claims_tenant_id_dependent_type_dependent_id",
                schema: "transactions",
                table: "reconciliation_claims",
                columns: new[] { "tenant_id", "dependent_type", "dependent_id" },
                unique: true,
                filter: "status <> 'released'");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_claims_tenant_id_transaction_id",
                schema: "transactions",
                table: "reconciliation_claims",
                columns: new[] { "tenant_id", "transaction_id" },
                unique: true,
                filter: "status <> 'released'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reconciliation_claims",
                schema: "transactions");
        }
    }
}
