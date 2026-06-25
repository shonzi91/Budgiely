using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseOnBehalfOfOtherAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnBehalfOfOtherAccount",
                table: "Expenses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnBehalfOfOtherAccount",
                table: "Expenses");
        }
    }
}
