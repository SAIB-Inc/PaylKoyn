using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.ImageGen.Migrations
{
    /// <inheritdoc />
    public partial class Airdrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AirdropTxHash",
                table: "MintRequests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AirdropTxHash",
                table: "MintRequests");
        }
    }
}
