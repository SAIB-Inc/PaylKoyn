using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionSubmissions",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "text", nullable: false),
                    TxRaw = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DateSubmitted = table.Column<long>(type: "bigint", nullable: false),
                    ConfirmedSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionSubmissions", x => x.Hash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsBySlot_Slot",
                table: "TransactionsBySlot",
                column: "Slot");

            migrationBuilder.CreateIndex(
                name: "IX_OutputsBySlot_Slot",
                table: "OutputsBySlot",
                column: "Slot");

            migrationBuilder.CreateIndex(
                name: "IX_OutputsBySlot_SpentSlot",
                table: "OutputsBySlot",
                column: "SpentSlot");

            migrationBuilder.CreateIndex(
                name: "IX_OutputsBySlot_SpentTxHash",
                table: "OutputsBySlot",
                column: "SpentTxHash");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSubmissions_DateSubmitted",
                table: "TransactionSubmissions",
                column: "DateSubmitted");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSubmissions_Status",
                table: "TransactionSubmissions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_TransactionsBySlot_Slot",
                table: "TransactionsBySlot");

            migrationBuilder.DropIndex(
                name: "IX_OutputsBySlot_Slot",
                table: "OutputsBySlot");

            migrationBuilder.DropIndex(
                name: "IX_OutputsBySlot_SpentSlot",
                table: "OutputsBySlot");

            migrationBuilder.DropIndex(
                name: "IX_OutputsBySlot_SpentTxHash",
                table: "OutputsBySlot");
        }
    }
}
