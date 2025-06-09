using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaylKoyn.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "OutputsBySlot",
                schema: "public",
                columns: table => new
                {
                    OutRef = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SpentTxHash = table.Column<string>(type: "text", nullable: false),
                    SpentSlot = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: false),
                    BlockHash = table.Column<string>(type: "text", nullable: false),
                    ScriptDataHash = table.Column<string>(type: "text", nullable: true),
                    ScriptHash = table.Column<string>(type: "text", nullable: true),
                    Raw = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutputsBySlot", x => x.OutRef);
                });

            migrationBuilder.CreateTable(
                name: "ReducerStates",
                schema: "public",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LatestIntersectionsJson = table.Column<string>(type: "text", nullable: false),
                    StartIntersectionJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReducerStates", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "TransactionsBySlot",
                schema: "public",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Slot = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Metadata = table.Column<byte[]>(type: "bytea", nullable: false),
                    Body = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionsBySlot", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "TransactionSubmissions",
                schema: "public",
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
                name: "IX_OutputsBySlot_Slot",
                schema: "public",
                table: "OutputsBySlot",
                column: "Slot");

            migrationBuilder.CreateIndex(
                name: "IX_OutputsBySlot_SpentSlot",
                schema: "public",
                table: "OutputsBySlot",
                column: "SpentSlot");

            migrationBuilder.CreateIndex(
                name: "IX_OutputsBySlot_SpentTxHash",
                schema: "public",
                table: "OutputsBySlot",
                column: "SpentTxHash");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsBySlot_Slot",
                schema: "public",
                table: "TransactionsBySlot",
                column: "Slot");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSubmissions_DateSubmitted",
                schema: "public",
                table: "TransactionSubmissions",
                column: "DateSubmitted");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSubmissions_Status",
                schema: "public",
                table: "TransactionSubmissions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutputsBySlot",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ReducerStates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TransactionsBySlot",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TransactionSubmissions",
                schema: "public");
        }
    }
}
