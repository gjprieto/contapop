using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBookkeepingReplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "expenses");

            migrationBuilder.EnsureSchema(
                name: "revenues");

            migrationBuilder.EnsureSchema(
                name: "planning");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "expenses",
                columns: table => new
                {
                    consumer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.consumer_name, x.event_id });
                });

            migrationBuilder.CreateTable(
                name: "project_replica",
                schema: "expenses",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_replica", x => x.project_id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_replica",
                schema: "expenses",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_replica", x => x.transaction_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_replica_tenant_id_status",
                schema: "expenses",
                table: "project_replica",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_replica_tenant_id_status",
                schema: "expenses",
                table: "transaction_replica",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "project_replica",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "transaction_replica",
                schema: "expenses");
        }
    }
}
