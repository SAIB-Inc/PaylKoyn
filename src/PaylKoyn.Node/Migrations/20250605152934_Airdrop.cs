using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Node.Migrations
{
    /// <inheritdoc />
    public partial class Airdrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AirdropAddress",
                table: "Wallets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AirdropTxHash",
                table: "Wallets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AirdropAddress",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "AirdropTxHash",
                table: "Wallets");
        }
    }
}
