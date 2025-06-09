using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Node.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTempFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Wallets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Wallets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Wallets",
                type: "TEXT",
                nullable: true);
        }
    }
}
