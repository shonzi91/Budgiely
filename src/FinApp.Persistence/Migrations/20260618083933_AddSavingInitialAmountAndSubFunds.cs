using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingInitialAmountAndSubFunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InitialAmount",
                table: "SavingCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Funds",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialAmount",
                table: "SavingCategories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Funds");
        }
    }
}
