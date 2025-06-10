using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Node.Migrations
{
    /// <inheritdoc />
    public partial class Refund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AirdropAddress",
                table: "Wallets",
                newName: "UserAddress");

            migrationBuilder.AddColumn<int>(
                name: "LastValidStatus",
                table: "Wallets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundTxHash",
                table: "Wallets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastValidStatus",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "RefundTxHash",
                table: "Wallets");

            migrationBuilder.RenameColumn(
                name: "UserAddress",
                table: "Wallets",
                newName: "AirdropAddress");
        }
    }
}
