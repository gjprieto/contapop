using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Contapop.Reconciliation.Service.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinatorState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "claim_version",
                schema: "reconciliation",
                table: "reconciliation_operations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expected_dependent_version",
                schema: "reconciliation",
                table: "reconciliation_operations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "claim_version",
                schema: "reconciliation",
                table: "reconciliation_operations");

            migrationBuilder.DropColumn(
                name: "expected_dependent_version",
                schema: "reconciliation",
                table: "reconciliation_operations");
        }
    }
}
