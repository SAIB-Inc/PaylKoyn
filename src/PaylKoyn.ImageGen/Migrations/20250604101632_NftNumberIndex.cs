using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.ImageGen.Migrations
{
    /// <inheritdoc />
    public partial class NftNumberIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MintRequests_NftNumber",
                table: "MintRequests",
                column: "NftNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MintRequests_NftNumber",
                table: "MintRequests");
        }
    }
}
