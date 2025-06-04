using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.ImageGen.Migrations
{
    /// <inheritdoc />
    public partial class NftNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NftNumber",
                table: "MintRequests",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NftNumber",
                table: "MintRequests");
        }
    }
}
