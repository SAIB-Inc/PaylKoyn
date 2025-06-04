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
            migrationBuilder.CreateTable(
                name: "OutputsBySlot",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutputsBySlot");

            migrationBuilder.DropTable(
                name: "ReducerStates");

            migrationBuilder.DropTable(
                name: "TransactionsBySlot");
        }
    }
}
