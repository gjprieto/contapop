using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Ledger.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDispatchState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_error",
                schema: "bank_accounts",
                table: "outbox_messages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                schema: "bank_accounts",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publish_attempts",
                schema: "bank_accounts",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                schema: "bank_accounts",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_error",
                schema: "bank_accounts",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "locked_until",
                schema: "bank_accounts",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "publish_attempts",
                schema: "bank_accounts",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "published_at",
                schema: "bank_accounts",
                table: "outbox_messages");
        }
    }
}
