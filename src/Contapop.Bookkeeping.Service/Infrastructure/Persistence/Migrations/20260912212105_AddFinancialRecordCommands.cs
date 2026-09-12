using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialRecordCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "revenues");

            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciled_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recurring = table.Column<bool>(type: "boolean", nullable: false),
                    recurring_interval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    import_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "expenses",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => new { x.tenant_id, x.operation, x.key });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "expenses",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "revenues",
                schema: "revenues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciled_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recurring = table.Column<bool>(type: "boolean", nullable: false),
                    recurring_interval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    import_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenues", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_tenant_id_project_id_date",
                schema: "expenses",
                table: "expenses",
                columns: new[] { "tenant_id", "project_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_tenant_id_reconciled_transaction_id",
                schema: "expenses",
                table: "expenses",
                columns: new[] { "tenant_id", "reconciled_transaction_id" },
                unique: true,
                filter: "reconciled_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_status_occurred_at",
                schema: "expenses",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_revenues_tenant_id_project_id_date",
                schema: "revenues",
                table: "revenues",
                columns: new[] { "tenant_id", "project_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_revenues_tenant_id_reconciled_transaction_id",
                schema: "revenues",
                table: "revenues",
                columns: new[] { "tenant_id", "reconciled_transaction_id" },
                unique: true,
                filter: "reconciled_transaction_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expenses",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "revenues",
                schema: "revenues");
        }
    }
}
