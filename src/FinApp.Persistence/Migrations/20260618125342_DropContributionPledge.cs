using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropContributionPledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "Pledged",
                table: "Contributions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Contributions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pledged",
                table: "Contributions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
