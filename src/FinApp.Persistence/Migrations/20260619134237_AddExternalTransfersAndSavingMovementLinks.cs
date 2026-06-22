using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalTransfersAndSavingMovementLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BudgetCategoryId",
                table: "SavingAllocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferPairId",
                table: "SavingAllocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    PeriodId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalTransfers_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTransfers_PeriodId",
                table: "ExternalTransfers",
                column: "PeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalTransfers");

            migrationBuilder.DropColumn(
                name: "BudgetCategoryId",
                table: "SavingAllocations");

            migrationBuilder.DropColumn(
                name: "TransferPairId",
                table: "SavingAllocations");
        }
    }
}
