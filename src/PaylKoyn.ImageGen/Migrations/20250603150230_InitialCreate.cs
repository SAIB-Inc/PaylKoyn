using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.ImageGen.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MintRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WalletIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    UserAddress = table.Column<string>(type: "TEXT", nullable: false),
                    UploadPaymentAddress = table.Column<string>(type: "TEXT", nullable: true),
                    UploadPaymentAmount = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PolicyId = table.Column<string>(type: "TEXT", nullable: true),
                    AssetName = table.Column<string>(type: "TEXT", nullable: true),
                    NftMetadata = table.Column<string>(type: "TEXT", nullable: true),
                    AdaFsId = table.Column<string>(type: "TEXT", nullable: true),
                    MintTxHash = table.Column<string>(type: "TEXT", nullable: true),
                    Traits = table.Column<string>(type: "TEXT", nullable: true),
                    Image = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MintRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MintRequests");
        }
    }
}
