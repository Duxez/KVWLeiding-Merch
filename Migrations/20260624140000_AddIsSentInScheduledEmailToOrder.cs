using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kvwleidingmerch.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSentInScheduledEmailToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSentInScheduledEmail",
                table: "Orders",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSentInScheduledEmail",
                table: "Orders");
        }
    }
}
