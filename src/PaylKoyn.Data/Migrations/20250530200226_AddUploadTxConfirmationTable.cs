using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadTxConfirmationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadTxConfirmations",
                columns: table => new
                {
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    TxHash = table.Column<string>(type: "TEXT", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TxConfirmationsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadTxConfirmations", x => x.Address);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadTxConfirmations");
        }
    }
}
