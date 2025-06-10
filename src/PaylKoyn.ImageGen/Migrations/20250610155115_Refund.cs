using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.ImageGen.Migrations
{
    /// <inheritdoc />
    public partial class Refund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastValidStatus",
                table: "MintRequests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundTxHash",
                table: "MintRequests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastValidStatus",
                table: "MintRequests");

            migrationBuilder.DropColumn(
                name: "RefundTxHash",
                table: "MintRequests");
        }
    }
}
