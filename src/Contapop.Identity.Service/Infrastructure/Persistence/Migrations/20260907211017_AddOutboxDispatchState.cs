using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Identity.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDispatchState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                schema: "tenancy",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "locked_until",
                schema: "tenancy",
                table: "outbox_messages");
        }
    }
}
