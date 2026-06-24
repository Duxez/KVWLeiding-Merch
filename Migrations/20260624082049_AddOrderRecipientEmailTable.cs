using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace kvwleidingmerch.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderRecipientEmailTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderRecipientEmails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderRecipientEmails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderRecipientEmails_EmailAddress",
                table: "OrderRecipientEmails",
                column: "EmailAddress",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderRecipientEmails");
        }
    }
}
